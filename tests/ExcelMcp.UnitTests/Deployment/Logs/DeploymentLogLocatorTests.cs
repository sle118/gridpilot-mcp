using ExcelMcp.Deployment.Logs;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.UnitTests.Deployment.Logs;

public sealed class DeploymentLogLocatorTests
{
    [Fact]
    public void Locate_IncludesConfiguredLogPath()
    {
        using var temp = TestProfileWorkspace.Create();
        var configuredLogPath = Path.Combine(temp.DirectoryPath, "configured.log");
        var profile = temp.BuildProfile(logPath: configuredLogPath);

        var logs = DeploymentLogLocator.Locate(profile);

        var configured = Assert.Single(logs, log => log.Kind == DeploymentLogKind.ProfileConfigured);
        Assert.Equal(configuredLogPath, configured.Path);
        Assert.False(configured.Exists);
        Assert.Equal(DeploymentLogAccessStatus.Missing, configured.AccessStatus);
    }

    [Fact]
    public void Locate_IncludesGridPilotLogPathFromEnvironment()
    {
        using var temp = TestProfileWorkspace.Create();
        var envLogPath = Path.Combine(temp.DirectoryPath, "env.log");
        var profile = temp.BuildProfile(env: new Dictionary<string, string?>
        {
            ["GRIDPILOT_LOG_LEVEL"] = "info",
            ["GRIDPILOT_LOG_PATH"] = envLogPath
        });

        var logs = DeploymentLogLocator.Locate(profile);

        var environment = Assert.Single(logs, log => log.Kind == DeploymentLogKind.HostEnvironment);
        Assert.Equal(envLogPath, environment.Path);
    }

    [Fact]
    public void Locate_IncludesConventionalRuntimeLogFromWorkingDirectory()
    {
        using var temp = TestProfileWorkspace.Create();

        var logs = DeploymentLogLocator.Locate(temp.BuildProfile());

        var conventional = Assert.Single(logs, log => log.Kind == DeploymentLogKind.HostConventional);
        Assert.Equal(Path.Combine(temp.DirectoryPath, ".tmp", "gridpilot-runtime.log"), conventional.Path);
    }

    [Fact]
    public void Locate_IncludesDeterministicProxyFallback()
    {
        using var temp = TestProfileWorkspace.Create();

        var logs = DeploymentLogLocator.Locate(temp.BuildProfile());

        var proxy = Assert.Single(logs, log => log.Kind == DeploymentLogKind.ProxyConventional);
        Assert.Equal(Path.Combine(temp.DirectoryPath, ".tmp", "mcp-proxy", "gridpilot-default.log"), proxy.Path);
    }

    [Fact]
    public void Locate_ReportsExistingLogMetadata()
    {
        using var temp = TestProfileWorkspace.Create();
        var configuredLogPath = Path.Combine(temp.DirectoryPath, "configured.log");
        File.WriteAllText(configuredLogPath, "hello");
        var profile = temp.BuildProfile(logPath: configuredLogPath);

        var logs = DeploymentLogLocator.Locate(profile);

        var configured = Assert.Single(logs, log => log.Kind == DeploymentLogKind.ProfileConfigured);
        Assert.True(configured.Exists);
        Assert.Equal(5, configured.SizeBytes);
        Assert.NotNull(configured.LastWriteTimeUtc);
        Assert.Equal(DeploymentLogAccessStatus.Accessible, configured.AccessStatus);
    }

    [Fact]
    public void Locate_ReportsLockedLogAsUnreadable()
    {
        using var temp = TestProfileWorkspace.Create();
        var configuredLogPath = Path.Combine(temp.DirectoryPath, "locked.log");
        File.WriteAllText(configuredLogPath, "locked");
        using var locked = new FileStream(configuredLogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var profile = temp.BuildProfile(logPath: configuredLogPath);

        var logs = DeploymentLogLocator.Locate(profile);

        var configured = Assert.Single(logs, log => log.Kind == DeploymentLogKind.ProfileConfigured);
        Assert.True(configured.Exists);
        Assert.Equal(DeploymentLogAccessStatus.Unreadable, configured.AccessStatus);
        Assert.False(string.IsNullOrWhiteSpace(configured.Message));
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
