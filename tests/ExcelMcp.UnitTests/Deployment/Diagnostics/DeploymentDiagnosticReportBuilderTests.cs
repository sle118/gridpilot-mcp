using ExcelMcp.Deployment.Diagnostics;
using ExcelMcp.Deployment.Logs;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.UnitTests.Deployment.Diagnostics;

public sealed class DeploymentDiagnosticReportBuilderTests
{
    [Fact]
    public async Task BuildAsync_RedactsSensitiveEnvironmentValues()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile(env: new Dictionary<string, string?>
        {
            ["API_KEY"] = "super-secret",
            ["GRIDPILOT_LOG_LEVEL"] = "info"
        });

        var report = await DeploymentDiagnosticReportBuilder.BuildAsync(profile);

        Assert.Contains("- API_KEY: <redacted>", report.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", report.Content, StringComparison.Ordinal);
        Assert.Contains("- GRIDPILOT_LOG_LEVEL: info", report.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_IncludesHostStdoutPolicyAndLogMetadata()
    {
        using var temp = TestProfileWorkspace.Create();
        var logPath = Path.Combine(temp.DirectoryPath, "runtime.log");
        File.WriteAllText(logPath, "runtime log line");
        var profile = temp.BuildProfile(logPath: logPath);

        var report = await DeploymentDiagnosticReportBuilder.BuildAsync(profile);

        Assert.Contains("- Command: " + temp.CommandPath, report.Content, StringComparison.Ordinal);
        Assert.Contains("- Args: --session-mode attach --attach-target workbook-owner", report.Content, StringComparison.Ordinal);
        Assert.Contains("- Stdout policy: jsonRpcOnly", report.Content, StringComparison.Ordinal);
        Assert.Contains("profile-configured: " + logPath, report.Content, StringComparison.Ordinal);
        Assert.Contains("Exists: true", report.Content, StringComparison.Ordinal);
        Assert.Contains("Access: Accessible", report.Content, StringComparison.Ordinal);
        Assert.Contains("Recent log tails were not included.", report.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime log line", report.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_OptionallyIncludesBoundedRecentLogTails()
    {
        using var temp = TestProfileWorkspace.Create();
        var logPath = Path.Combine(temp.DirectoryPath, "runtime.log");
        File.WriteAllText(logPath, "one\ntwo\nthree");
        var profile = temp.BuildProfile(logPath: logPath);

        var report = await DeploymentDiagnosticReportBuilder.BuildAsync(
            profile,
            new DeploymentDiagnosticReportOptions
            {
                IncludeRecentLogTails = true,
                RecentLogOptions = new RecentLogReadOptions(MaxLines: 2, MaxBytes: 1024)
            });

        Assert.NotEmpty(report.RecentLogTails);
        Assert.Contains("## Recent Log Tails", report.Content, StringComparison.Ordinal);
        Assert.Contains("two", report.Content, StringComparison.Ordinal);
        Assert.Contains("three", report.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("one", report.Content, StringComparison.Ordinal);
    }

    private sealed class TestProfileWorkspace : IDisposable
    {
        private TestProfileWorkspace(string directoryPath, string commandPath)
        {
            DirectoryPath = directoryPath;
            CommandPath = commandPath;
        }

        public string DirectoryPath { get; }

        public string CommandPath { get; }

        public static TestProfileWorkspace Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var commandPath = Path.Combine(directoryPath, "ExcelMcp.ToolHost.exe");
            File.WriteAllText(commandPath, string.Empty);
            return new TestProfileWorkspace(directoryPath, commandPath);
        }

        public LaunchProfile BuildProfile(
            string? logPath = null,
            IReadOnlyDictionary<string, string?>? env = null) =>
            new()
            {
                SchemaVersion = 1,
                Name = "gridpilot-default",
                DisplayName = "GridPilot MCP",
                Host = new LaunchProfileHost
                {
                    Command = CommandPath,
                    Args = ["--session-mode", "attach", "--attach-target", "workbook-owner"],
                    WorkingDirectory = DirectoryPath,
                    Env = env ?? new Dictionary<string, string?> { ["GRIDPILOT_LOG_LEVEL"] = "info" }
                },
                Logs = new LaunchProfileLogs
                {
                    Path = logPath,
                    StdoutPolicy = LaunchProfileValidator.JsonRpcOnlyStdoutPolicy
                }
            };

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
