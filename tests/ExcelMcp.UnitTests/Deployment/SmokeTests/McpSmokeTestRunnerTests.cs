using System.Text;
using System.Text.Json;
using ExcelMcp.Deployment.SmokeTests;

namespace ExcelMcp.UnitTests.Deployment.SmokeTests;

public sealed class McpSmokeTestRunnerTests
{
    [Fact]
    public async Task RunAsync_FramedInitializeAndToolsListSuccess()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(Frames(
            Framed(InitializeResponse()),
            Framed(ToolsListResponse(McpSmokeTestOptions.DefaultExpectedToolNames)),
            Framed(ShutdownResponse())));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.True(report.IsSuccess);
        Assert.Equal(McpSmokeTestTransportMode.Framed, report.DetectedTransportMode);
        Assert.Empty(report.MissingToolNames);
        Assert.Contains(report.Results, result => result.Id == "mcp.initialize" && result.Status == McpSmokeTestStatus.Success);
        Assert.Contains(report.Results, result => result.Id == "mcp.toolsList" && result.Status == McpSmokeTestStatus.Success);
        Assert.Contains(report.Results, result => result.Id == "process.shutdown" && result.Status == McpSmokeTestStatus.Success);
        Assert.Equal(temp.CommandPath, launcher.StartInfo!.Command);
    }

    [Fact]
    public async Task RunAsync_RawJsonInitializeAndToolsListSuccess()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(Frames(
            Raw(InitializeResponse()),
            Raw(ToolsListResponse(McpSmokeTestOptions.DefaultExpectedToolNames)),
            Raw(ShutdownResponse())));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(
            temp.WriteProfile(),
            new McpSmokeTestOptions { RequestTransportMode = McpSmokeTestTransportMode.RawJson });

        Assert.True(report.IsSuccess);
        Assert.Equal(McpSmokeTestTransportMode.RawJson, report.DetectedTransportMode);
    }

    [Theory]
    [InlineData("\r\n\r\n")]
    [InlineData("\n\n")]
    public async Task RunAsync_ReadsFramedHeaderTerminators(string terminator)
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(Frames(
            Framed(InitializeResponse(), terminator),
            Framed(ToolsListResponse(McpSmokeTestOptions.DefaultExpectedToolNames), terminator),
            Framed(ShutdownResponse(), terminator)));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.True(report.IsSuccess);
        Assert.Equal(McpSmokeTestTransportMode.Framed, report.DetectedTransportMode);
    }

    [Fact]
    public async Task RunAsync_DetectsStdoutPollutionBeforeFramedResponse()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(Encoding.UTF8.GetBytes("diagnostic\n" + Framed(InitializeResponse())));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.Contains(report.Results, result =>
            result.Id == "mcp.stdoutPollution" &&
            result.Status == McpSmokeTestStatus.Failure);
    }

    [Fact]
    public async Task RunAsync_DetectsStdoutPollutionBeforeRawJsonResponse()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(Encoding.UTF8.GetBytes("oops " + Raw(InitializeResponse())));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(
            temp.WriteProfile(),
            new McpSmokeTestOptions { RequestTransportMode = McpSmokeTestTransportMode.RawJson });

        Assert.Contains(report.Results, result =>
            result.Id == "mcp.stdoutPollution" &&
            result.Status == McpSmokeTestStatus.Failure);
    }

    [Fact]
    public async Task RunAsync_InvalidJsonResponseFails()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(Encoding.UTF8.GetBytes("Content-Length: 5\r\n\r\n{bad}"));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.Contains(report.Results, result =>
            result.Id == "mcp.invalidJson" &&
            result.Status == McpSmokeTestStatus.Failure);
    }

    [Fact]
    public async Task RunAsync_NoResponseTimeoutKillsProcess()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithProcess(new FakeProcess(new BlockingReadStream()));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(
            temp.WriteProfile(),
            new McpSmokeTestOptions { Timeout = TimeSpan.FromMilliseconds(25), ShutdownTimeout = TimeSpan.FromMilliseconds(25) });

        Assert.True(report.WasKilled);
        Assert.Contains(report.Results, result =>
            result.Id == "mcp.timeout" &&
            result.Status == McpSmokeTestStatus.Timeout);
    }

    [Fact]
    public async Task RunAsync_ProcessLaunchFailureIsReported()
    {
        using var temp = SmokeTestWorkspace.Create();
        var runner = new McpSmokeTestRunner(FakeProcessLauncher.Throwing());

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.Contains(report.Results, result =>
            result.Id == "process.launch" &&
            result.Status == McpSmokeTestStatus.Failure);
    }

    [Fact]
    public async Task RunAsync_PrematureProcessExitIsReported()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithProcess(new FakeProcess(new MemoryStream(), hasExited: true, exitCode: 3));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.Equal(3, report.ExitCode);
        Assert.Contains(report.Results, result =>
            result.Id == "process.exited" &&
            result.Status == McpSmokeTestStatus.Failure);
    }

    [Fact]
    public async Task RunAsync_MissingExpectedToolsIsReported()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(Frames(
            Framed(InitializeResponse()),
            Framed(ToolsListResponse(["session_list_open_workbooks"])),
            Framed(ShutdownResponse())));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.False(report.IsSuccess);
        Assert.Contains("range_read", report.MissingToolNames);
        Assert.Contains(report.Results, result =>
            result.Id == "mcp.toolsList" &&
            result.Status == McpSmokeTestStatus.Failure);
    }

    [Fact]
    public async Task RunAsync_CapturesStderrTail()
    {
        using var temp = SmokeTestWorkspace.Create();
        var launcher = FakeProcessLauncher.WithStdout(
            Frames(
                Framed(InitializeResponse()),
                Framed(ToolsListResponse(McpSmokeTestOptions.DefaultExpectedToolNames)),
                Framed(ShutdownResponse())),
            stderr: "warning on stderr");
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(temp.WriteProfile());

        Assert.Contains("warning on stderr", report.StderrTail, StringComparison.Ordinal);
        Assert.Contains(report.Results, result =>
            result.Id == "process.stderr" &&
            result.Status == McpSmokeTestStatus.Warning);
    }

    [Fact]
    public async Task RunAsync_KillsProcessWhenShutdownTimesOut()
    {
        using var temp = SmokeTestWorkspace.Create();
        var stdout = new PrefixThenBlockingStream(Frames(
            Framed(InitializeResponse()),
            Framed(ToolsListResponse(McpSmokeTestOptions.DefaultExpectedToolNames))));
        var launcher = FakeProcessLauncher.WithProcess(new FakeProcess(stdout, waitCompletes: false));
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(
            temp.WriteProfile(),
            new McpSmokeTestOptions { Timeout = TimeSpan.FromSeconds(1), ShutdownTimeout = TimeSpan.FromMilliseconds(25) });

        Assert.True(report.WasKilled);
        Assert.Contains(report.Results, result =>
            result.Id == "process.shutdown" &&
            result.Status == McpSmokeTestStatus.Warning);
    }

    [Fact]
    public async Task RunAsync_InvalidProfileReturnsFailureWithoutLaunch()
    {
        using var temp = SmokeTestWorkspace.Create();
        var profilePath = temp.WriteProfile(stdoutPolicy: "dirtyStdout");
        var launcher = FakeProcessLauncher.WithStdout(Array.Empty<byte>());
        var runner = new McpSmokeTestRunner(launcher);

        var report = await runner.RunAsync(profilePath);

        Assert.Equal(0, launcher.LaunchCount);
        Assert.Contains(report.Results, result =>
            result.Id == "profile.validation.unsupported_stdout_policy" &&
            result.Status == McpSmokeTestStatus.Failure);
    }

    private static string InitializeResponse() =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { tools = new { listChanged = false } },
                serverInfo = new { name = "GridPilot MCP", version = "0.1.0" }
            }
        });

    private static string ToolsListResponse(IEnumerable<string> names) =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 2,
            result = new
            {
                tools = names.Select(name => new
                {
                    name,
                    description = name,
                    inputSchema = new { type = "object", properties = new { } }
                })
            }
        });

    private static string ShutdownResponse() =>
        JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 3,
            result = new { }
        });

    private static string Framed(string body, string terminator = "\r\n\r\n") =>
        $"Content-Length: {Encoding.UTF8.GetByteCount(body)}{terminator}{body}";

    private static string Raw(string body) => body + "\n";

    private static byte[] Frames(params string[] frames) =>
        Encoding.UTF8.GetBytes(string.Concat(frames));

    private sealed class FakeProcessLauncher : IMcpSmokeTestProcessLauncher
    {
        private readonly FakeProcess? _process;
        private readonly bool _throwOnLaunch;

        private FakeProcessLauncher(FakeProcess? process = null, bool throwOnLaunch = false)
        {
            _process = process;
            _throwOnLaunch = throwOnLaunch;
        }

        public int LaunchCount { get; private set; }

        public McpSmokeTestProcessStartInfo? StartInfo { get; private set; }

        public static FakeProcessLauncher WithStdout(byte[] stdout, string stderr = "") =>
            WithProcess(new FakeProcess(new MemoryStream(stdout), stderr: stderr));

        public static FakeProcessLauncher WithProcess(FakeProcess process) => new(process);

        public static FakeProcessLauncher Throwing() => new(throwOnLaunch: true);

        public IMcpSmokeTestProcess Launch(McpSmokeTestProcessStartInfo startInfo)
        {
            LaunchCount++;
            StartInfo = startInfo;
            if (_throwOnLaunch)
            {
                throw new InvalidOperationException("launch failed");
            }

            return _process ?? throw new InvalidOperationException("No fake process configured.");
        }
    }

    private sealed class FakeProcess : IMcpSmokeTestProcess
    {
        private readonly bool _waitCompletes;
        private bool _hasExited;

        public FakeProcess(
            Stream stdout,
            string stderr = "",
            bool hasExited = false,
            int? exitCode = null,
            bool waitCompletes = true)
        {
            StandardOutput = stdout;
            StandardError = new MemoryStream(Encoding.UTF8.GetBytes(stderr));
            _hasExited = hasExited;
            ExitCode = exitCode;
            _waitCompletes = waitCompletes;
        }

        public Stream StandardInput { get; } = new MemoryStream();

        public Stream StandardOutput { get; }

        public Stream StandardError { get; }

        public bool HasExited => _hasExited;

        public int? ExitCode { get; private set; }

        public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            if (_waitCompletes)
            {
                _hasExited = true;
                ExitCode ??= 0;
                return;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public void Kill()
        {
            _hasExited = true;
            ExitCode ??= -1;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    private sealed class PrefixThenBlockingStream : Stream
    {
        private readonly MemoryStream _prefix;
        private readonly BlockingReadStream _blocking = new();

        public PrefixThenBlockingStream(byte[] prefix)
        {
            _prefix = new MemoryStream(prefix);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_prefix.Position < _prefix.Length)
            {
                return await _prefix.ReadAsync(buffer, cancellationToken);
            }

            return await _blocking.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class SmokeTestWorkspace : IDisposable
    {
        private SmokeTestWorkspace(string directoryPath, string commandPath)
        {
            DirectoryPath = directoryPath;
            CommandPath = commandPath;
        }

        public string DirectoryPath { get; }

        public string CommandPath { get; }

        public static SmokeTestWorkspace Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var commandPath = Path.Combine(directoryPath, "GridPilotHost.exe");
            File.WriteAllText(commandPath, string.Empty);
            return new SmokeTestWorkspace(directoryPath, commandPath);
        }

        public string WriteProfile(string stdoutPolicy = "jsonRpcOnly")
        {
            var profilePath = Path.Combine(DirectoryPath, "profile.json");
            var json = JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    name = "gridpilot-default",
                    displayName = "GridPilot MCP",
                    host = new
                    {
                        command = CommandPath,
                        args = new[] { "--session-mode", "attach", "--attach-target", "workbook-owner" },
                        workingDirectory = DirectoryPath,
                        env = new Dictionary<string, string?>
                        {
                            ["GRIDPILOT_LOG_LEVEL"] = "info"
                        }
                    },
                    logs = new
                    {
                        path = (string?)null,
                        stdoutPolicy
                    }
                },
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(profilePath, json);
            return profilePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
