using System.Text;
using System.Text.Json;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.Deployment.SmokeTests;

public sealed class McpSmokeTestRunner
{
    private readonly IMcpSmokeTestProcessLauncher _processLauncher;

    public McpSmokeTestRunner(IMcpSmokeTestProcessLauncher? processLauncher = null)
    {
        _processLauncher = processLauncher ?? new ProcessMcpSmokeTestProcessLauncher();
    }

    public async Task<McpSmokeTestReport> RunAsync(
        string profilePath,
        McpSmokeTestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return FailureReport("profile.path", "Profile path", "Launch profile path is required.", "Pass the path to a GridPilot launch profile JSON file.");
        }

        var load = LaunchProfileLoader.Load(profilePath);
        if (load.Profile is null)
        {
            return FailureReport(load.Issues.Select(issue => FromProfileIssue($"profile.load.{issue.Code}", "Profile load", issue)));
        }

        return await RunAsync(load.Profile, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<McpSmokeTestReport> RunAsync(
        LaunchProfile profile,
        McpSmokeTestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        options ??= new McpSmokeTestOptions();

        var validation = LaunchProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            return FailureReport(validation.Issues.Select(issue => FromProfileIssue($"profile.validation.{issue.Code}", "Profile validation", issue)));
        }

        var results = new List<McpSmokeTestStepResult>
        {
            Success("profile.validation", "Profile validation", "Launch profile is valid.")
        };
        var missingToolNames = Array.Empty<string>();
        McpSmokeTestTransportMode? detectedTransportMode = null;
        IMcpSmokeTestProcess? process = null;
        Task<string>? stderrTask = null;
        var wasKilled = false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            process = _processLauncher.Launch(BuildStartInfo(profile));
            stderrTask = ReadTailAsync(process.StandardError, options.StderrTailMaxChars, timeout.Token);
            results.Add(Success("process.launch", "Process launch", "MCP host process launched."));

            var initialize = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = options.ProtocolVersion,
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "gridpilot-deployment-smoke-test",
                        version = "0.1.0"
                    }
                }
            };

            await McpStdioProtocol.WriteAsync(process.StandardInput, initialize, options.RequestTransportMode, timeout.Token)
                .ConfigureAwait(false);
            var initializeResponse = await McpStdioProtocol.ReadAsync(process.StandardOutput, timeout.Token).ConfigureAwait(false);
            detectedTransportMode = initializeResponse.TransportMode;
            ValidateJsonRpcResult(initializeResponse.Payload, 1, "initialize");
            results.Add(Success("mcp.initialize", "MCP initialize", "MCP initialize returned a valid JSON-RPC result."));

            var toolsList = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list"
            };

            await McpStdioProtocol.WriteAsync(process.StandardInput, toolsList, options.RequestTransportMode, timeout.Token)
                .ConfigureAwait(false);
            var toolsResponse = await McpStdioProtocol.ReadAsync(process.StandardOutput, timeout.Token).ConfigureAwait(false);
            detectedTransportMode ??= toolsResponse.TransportMode;
            var toolNames = ExtractToolNames(toolsResponse.Payload, 2);
            missingToolNames = options.ExpectedToolNames
                .Where(expected => !toolNames.Contains(expected, StringComparer.Ordinal))
                .ToArray();

            if (missingToolNames.Length == 0)
            {
                results.Add(Success("mcp.toolsList", "MCP tools/list", $"tools/list returned {toolNames.Count} tool(s) and all expected GridPilot tools were present."));
            }
            else
            {
                results.Add(Failure(
                    "mcp.toolsList",
                    "MCP tools/list",
                    $"tools/list is missing expected tool(s): {string.Join(", ", missingToolNames)}.",
                    "Verify the configured host is the current GridPilot MCP host build."));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            wasKilled = KillProcess(process);
            results.Add(Timeout("mcp.timeout", "MCP smoke test timeout", $"MCP smoke test exceeded {options.Timeout}.", "Check whether the host is blocked during startup or protocol handling."));
        }
        catch (McpStdoutPollutionException exception)
        {
            results.Add(Failure("mcp.stdoutPollution", "MCP stdout pollution", exception.Message, "Move diagnostics to stderr or file-backed logs so stdout remains JSON-RPC only."));
        }
        catch (JsonException exception)
        {
            results.Add(Failure("mcp.invalidJson", "MCP JSON response", $"MCP response JSON is invalid: {exception.Message}", "Inspect host stdout and runtime logs for protocol corruption."));
        }
        catch (EndOfStreamException exception)
        {
            results.Add(Failure("process.exited", "MCP process exited", exception.Message, "Inspect stderr and runtime logs for host startup or protocol failures."));
        }
        catch (Exception exception) when (process is null)
        {
            results.Add(Failure("process.launch", "Process launch", $"Failed to launch MCP host process: {exception.Message}", "Verify the launch profile command, working directory, and permissions."));
        }
        catch (Exception exception)
        {
            results.Add(Failure("mcp.protocol", "MCP protocol", exception.Message, "Inspect stderr and runtime logs for protocol failures."));
        }
        finally
        {
            if (process is not null)
            {
                wasKilled = await CleanupAsync(process, options, wasKilled, results).ConfigureAwait(false);
            }
        }

        var stderrTail = stderrTask is null
            ? string.Empty
            : await CompleteStderrTailAsync(stderrTask).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(stderrTail))
        {
            results.Add(Warning("process.stderr", "Process stderr", "MCP host wrote to stderr during the smoke test.", "Inspect the stderr tail and runtime logs."));
        }

        return new McpSmokeTestReport(
            results,
            detectedTransportMode,
            process?.ExitCode,
            wasKilled,
            stderrTail,
            missingToolNames);
    }

    private static McpSmokeTestProcessStartInfo BuildStartInfo(LaunchProfile profile)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in profile.Host!.Env!)
        {
            env[key] = value!;
        }

        return new McpSmokeTestProcessStartInfo(
            profile.Host.Command!,
            profile.Host.Args!,
            profile.Host.WorkingDirectory,
            env);
    }

    private static void ValidateJsonRpcResult(string payload, int expectedId, string methodName)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!TryGetResponseId(root, out var id) || id != expectedId)
        {
            throw new McpSmokeTestProtocolException($"MCP {methodName} response did not contain the expected JSON-RPC id {expectedId}.");
        }

        if (root.TryGetProperty("error", out var error))
        {
            throw new McpSmokeTestProtocolException($"MCP {methodName} returned an error: {error}.");
        }

        if (!root.TryGetProperty("result", out var result) || result.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new McpSmokeTestProtocolException($"MCP {methodName} response did not contain a result.");
        }
    }

    private static IReadOnlyList<string> ExtractToolNames(string payload, int expectedId)
    {
        ValidateJsonRpcResult(payload, expectedId, "tools/list");
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("tools", out var tools) ||
            tools.ValueKind != JsonValueKind.Array)
        {
            throw new McpSmokeTestProtocolException("MCP tools/list response did not contain result.tools array.");
        }

        var names = new List<string>();
        foreach (var tool in tools.EnumerateArray())
        {
            if (tool.ValueKind == JsonValueKind.Object &&
                tool.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String &&
                nameElement.GetString() is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static bool TryGetResponseId(JsonElement root, out int id)
    {
        id = 0;
        if (!root.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        if (idElement.ValueKind == JsonValueKind.Number && idElement.TryGetInt32(out id))
        {
            return true;
        }

        if (idElement.ValueKind == JsonValueKind.String && int.TryParse(idElement.GetString(), out id))
        {
            return true;
        }

        return false;
    }

    private static async Task<bool> CleanupAsync(
        IMcpSmokeTestProcess process,
        McpSmokeTestOptions options,
        bool alreadyKilled,
        List<McpSmokeTestStepResult> results)
    {
        if (alreadyKilled)
        {
            await process.DisposeAsync().ConfigureAwait(false);
            return true;
        }

        if (process.HasExited)
        {
            results.Add(Success("process.exit", "Process exit", "MCP host process exited before cleanup."));
            await process.DisposeAsync().ConfigureAwait(false);
            return false;
        }

        using var shutdownTimeout = new CancellationTokenSource(options.ShutdownTimeout);
        try
        {
            await McpStdioProtocol.WriteAsync(
                process.StandardInput,
                new { jsonrpc = "2.0", id = 3, method = "shutdown" },
                options.RequestTransportMode,
                shutdownTimeout.Token).ConfigureAwait(false);
            var shutdownResponse = await McpStdioProtocol.ReadAsync(process.StandardOutput, shutdownTimeout.Token)
                .ConfigureAwait(false);
            ValidateJsonRpcResult(shutdownResponse.Payload, 3, "shutdown");
            await McpStdioProtocol.WriteAsync(
                process.StandardInput,
                new { jsonrpc = "2.0", method = "exit" },
                options.RequestTransportMode,
                shutdownTimeout.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(shutdownTimeout.Token).ConfigureAwait(false);
            results.Add(Success("process.shutdown", "Process shutdown", "MCP host process shut down cleanly."));
            await process.DisposeAsync().ConfigureAwait(false);
            return false;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or EndOfStreamException or McpSmokeTestProtocolException or McpStdoutPollutionException)
        {
            var killed = KillProcess(process);
            results.Add(Warning(
                "process.shutdown",
                "Process shutdown",
                $"MCP host process did not shut down cleanly: {exception.Message}",
                "Inspect runtime logs for shutdown handling; the smoke test killed the process."));
            await process.DisposeAsync().ConfigureAwait(false);
            return killed;
        }
    }

    private static bool KillProcess(IMcpSmokeTestProcess? process)
    {
        if (process is null || process.HasExited)
        {
            return false;
        }

        process.Kill();
        return true;
    }

    private static async Task<string> ReadTailAsync(Stream stream, int maxChars, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var builder = new StringBuilder();
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return builder.ToString();
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, read));
                if (builder.Length > maxChars)
                {
                    builder.Remove(0, builder.Length - maxChars);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return builder.ToString();
        }
        catch (IOException)
        {
            return builder.ToString();
        }
    }

    private static async Task<string> CompleteStderrTailAsync(Task<string> stderrTask)
    {
        var completed = await Task.WhenAny(stderrTask, Task.Delay(TimeSpan.FromMilliseconds(250))).ConfigureAwait(false);
        return completed == stderrTask
            ? await stderrTask.ConfigureAwait(false)
            : string.Empty;
    }

    private static McpSmokeTestReport FailureReport(string id, string name, string message, string nextStep) =>
        FailureReport([Failure(id, name, message, nextStep)]);

    private static McpSmokeTestReport FailureReport(IEnumerable<McpSmokeTestStepResult> results) =>
        new(results.ToArray(), null, null, false, string.Empty, Array.Empty<string>());

    private static McpSmokeTestStepResult FromProfileIssue(string id, string name, LaunchProfileIssue issue) =>
        new(
            id,
            name,
            issue.Severity == LaunchProfileIssueSeverity.Error ? McpSmokeTestStatus.Failure : McpSmokeTestStatus.Warning,
            issue.Message,
            "Fix the launch profile JSON and rerun the smoke test.");

    private static McpSmokeTestStepResult Success(string id, string name, string message) =>
        new(id, name, McpSmokeTestStatus.Success, message, "No action needed.");

    private static McpSmokeTestStepResult Warning(string id, string name, string message, string nextStep) =>
        new(id, name, McpSmokeTestStatus.Warning, message, nextStep);

    private static McpSmokeTestStepResult Failure(string id, string name, string message, string nextStep) =>
        new(id, name, McpSmokeTestStatus.Failure, message, nextStep);

    private static McpSmokeTestStepResult Timeout(string id, string name, string message, string nextStep) =>
        new(id, name, McpSmokeTestStatus.Timeout, message, nextStep);
}
