using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExcelMcp.Deployment.Installation;

namespace ExcelMcp.Deployment.AgentConfig;

public sealed class VsCodeMcpConfigWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly VsCodeUserMcpConfigPathLocator _pathLocator;

    public VsCodeMcpConfigWriter(VsCodeUserMcpConfigPathLocator? pathLocator = null)
    {
        _pathLocator = pathLocator ?? new VsCodeUserMcpConfigPathLocator();
    }

    public VsCodeMcpConfigWriteResult WriteForInstalledInstance(
        InstalledInstanceState install,
        string? configPath = null,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(install);

        var resolvedPath = string.IsNullOrWhiteSpace(configPath)
            ? _pathLocator.ResolvePath()
            : configPath;
        var issues = new List<AgentConfigIssue>();
        var summaryLines = new List<string>
        {
            $"VS Code MCP config path: {resolvedPath}"
        };

        if (!install.IsInstalled || !File.Exists(install.Paths.HostExecutablePath))
        {
            issues.Add(new AgentConfigIssue(
                AgentConfigIssueSeverity.Error,
                "installed_host_missing",
                $"Installed GridPilot host executable was not found at '{install.Paths.HostExecutablePath}'."));
            summaryLines.Add("No changes were written because the installed host executable was not found.");
            return Failed(resolvedPath, summaryLines, issues);
        }

        try
        {
            var currentText = File.Exists(resolvedPath)
                ? File.ReadAllText(resolvedPath, Encoding.UTF8)
                : string.Empty;
            JsonObject rootObject;
            JsonObject? existingRootObject = null;
            var action = File.Exists(resolvedPath)
                ? VsCodeMcpConfigWriteAction.Update
                : VsCodeMcpConfigWriteAction.Create;

            if (File.Exists(resolvedPath))
            {
                var parsed = JsonNode.Parse(currentText);
                if (parsed is not JsonObject existingRoot)
                {
                    issues.Add(new AgentConfigIssue(
                        AgentConfigIssueSeverity.Error,
                        "vscode_mcp_root_not_object",
                        "VS Code MCP config root must be a JSON object."));
                    summaryLines.Add("No changes were written because the existing config root is not a JSON object.");
                    return Failed(resolvedPath, summaryLines, issues);
                }

                existingRootObject = existingRoot;
                rootObject = existingRoot.DeepClone().AsObject();
            }
            else
            {
                rootObject = [];
            }

            var merged = MergeGridPilotServer(rootObject, install, issues);
            if (issues.Any(issue => issue.Severity == AgentConfigIssueSeverity.Error))
            {
                summaryLines.Add("No changes were written because the existing config shape is incompatible.");
                return Failed(resolvedPath, summaryLines, issues);
            }

            if (existingRootObject is not null &&
                JsonNode.DeepEquals(existingRootObject, merged))
            {
                summaryLines.Add("No changes required; servers.gridpilot already matches the installed GridPilot defaults.");
                return new VsCodeMcpConfigWriteResult(
                    resolvedPath,
                    BackupPath: null,
                    VsCodeMcpConfigWriteAction.NoChange,
                    WasWritten: false,
                    Diff: string.Empty,
                    summaryLines,
                    issues);
            }

            var updatedText = SerializeJson(merged);
            var diff = UnifiedTextDiff.Create(currentText, updatedText, resolvedPath, resolvedPath);

            summaryLines.Add(action == VsCodeMcpConfigWriteAction.Create
                ? "Create servers.gridpilot in the VS Code user MCP config."
                : "Update servers.gridpilot in the VS Code user MCP config.");

            if (dryRun)
            {
                summaryLines.Add("Dry run only; no files were written.");
                return new VsCodeMcpConfigWriteResult(
                    resolvedPath,
                    BackupPath: null,
                    action,
                    WasWritten: false,
                    diff,
                    summaryLines,
                    issues);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath) ?? ".");

            string? backupPath = null;
            if (File.Exists(resolvedPath))
            {
                backupPath = BuildBackupPath(resolvedPath);
                File.Copy(resolvedPath, backupPath, overwrite: false);
                summaryLines.Add($"Backup created: {backupPath}");
            }

            File.WriteAllText(resolvedPath, updatedText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            summaryLines.Add("Config file updated successfully.");
            return new VsCodeMcpConfigWriteResult(
                resolvedPath,
                backupPath,
                action,
                WasWritten: true,
                diff,
                summaryLines,
                issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new AgentConfigIssue(
                AgentConfigIssueSeverity.Error,
                "vscode_mcp_json_invalid",
                $"VS Code MCP config JSON could not be parsed: {ex.Message}"));
            summaryLines.Add("No changes were written because the existing config contains malformed JSON.");
            return Failed(resolvedPath, summaryLines, issues);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new AgentConfigIssue(
                AgentConfigIssueSeverity.Error,
                "vscode_mcp_write_failed",
                $"VS Code MCP config could not be written: {ex.Message}"));
            summaryLines.Add("No changes were written because the config file could not be updated.");
            return Failed(resolvedPath, summaryLines, issues);
        }
    }

    private static JsonObject MergeGridPilotServer(
        JsonObject rootObject,
        InstalledInstanceState install,
        List<AgentConfigIssue> issues)
    {
        if (rootObject["servers"] is JsonNode serversNode &&
            serversNode is not JsonObject)
        {
            issues.Add(new AgentConfigIssue(
                AgentConfigIssueSeverity.Error,
                "vscode_mcp_servers_not_object",
                "VS Code MCP config 'servers' property must be a JSON object."));
            return rootObject;
        }

        var serversObject = rootObject["servers"] as JsonObject ?? [];
        serversObject["gridpilot"] = BuildGridPilotServer(install);
        rootObject["servers"] = serversObject;
        return rootObject;
    }

    private static JsonObject BuildGridPilotServer(InstalledInstanceState install)
    {
        var launchDefaults = InstalledHostLaunchDefaultsBuilder.Build(install);
        var envObject = new JsonObject();
        foreach (var (key, value) in launchDefaults.Env.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            envObject[key] = value;
        }

        return new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = launchDefaults.Command,
            ["args"] = new JsonArray(launchDefaults.Args.Select(argument => JsonValue.Create(argument)!).ToArray()),
            ["env"] = envObject
        };
    }

    private static string SerializeJson(JsonObject rootObject) =>
        rootObject.ToJsonString(JsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string BuildBackupPath(string configPath)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss'Z'");
        return $"{configPath}.{timestamp}.bak";
    }

    private static VsCodeMcpConfigWriteResult Failed(
        string configPath,
        IReadOnlyList<string> summaryLines,
        IReadOnlyList<AgentConfigIssue> issues) =>
        new(
            configPath,
            BackupPath: null,
            VsCodeMcpConfigWriteAction.Failed,
            WasWritten: false,
            Diff: string.Empty,
            summaryLines,
            issues);
}
