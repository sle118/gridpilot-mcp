using System.Text.Json;
using ExcelMcp.Deployment.Installation;
using Xunit;

namespace GridPilot.Tray.Tests;

public sealed class TrayProfileContextTests
{
    [Fact]
    public void Resolve_UsesProfileArgumentBeforeEnvironment()
    {
        using var env = new EnvironmentVariableScope("GRIDPILOT_PROFILE_PATH", @"C:\env\profile.json");

        var context = TrayProfileContext.Resolve(["--profile", @"C:\cli\profile.json"]);

        Assert.Equal(@"C:\cli\profile.json", context.ProfilePath);
    }

    [Fact]
    public void Resolve_UsesEnvironmentWhenProfileArgumentMissing()
    {
        using var env = new EnvironmentVariableScope("GRIDPILOT_PROFILE_PATH", @"C:\env\profile.json");

        var context = TrayProfileContext.Resolve([]);

        Assert.Equal(@"C:\env\profile.json", context.ProfilePath);
    }

    [Fact]
    public void Resolve_UsesInstalledDefaultProfileWhenNoCliOrEnvironmentPathExists()
    {
        using var env = new EnvironmentVariableScope("GRIDPILOT_PROFILE_PATH", null);
        using var workspace = InstalledTrayWorkspace.Create();

        var context = TrayProfileContext.Resolve(
            ["--startup", "--no-dashboard"],
            workspace.TrayExecutablePath,
            workspace.InstallationService,
            new ProfileBootstrapService());

        Assert.Equal(workspace.DefaultProfilePath, context.ProfilePath);
        Assert.True(File.Exists(workspace.DefaultProfilePath));
        Assert.True(context.StartupLaunch);
        Assert.True(context.SuppressDashboard);
        Assert.False(context.OpenDashboardOnLaunch);
    }

    [Fact]
    public void Resolve_TracksOpenDashboardFlag()
    {
        var context = TrayProfileContext.Resolve(["--open-dashboard"]);

        Assert.True(context.OpenDashboardOnLaunch);
        Assert.False(context.SuppressDashboard);
    }

    [Fact]
    public void GetStatus_DisablesProfileActionsWhenNoProfileConfigured()
    {
        using var env = new EnvironmentVariableScope("GRIDPILOT_PROFILE_PATH", null);

        var status = TrayProfileContext.Resolve([]).GetStatus();

        Assert.Equal("No profile configured", status.Message);
        Assert.False(status.CanRunProfileActions);
    }

    [Fact]
    public void GetStatus_EnablesProfileActionsForValidProfile()
    {
        using var temp = TrayProfileTestWorkspace.Create();
        var context = new TrayProfileContext(temp.WriteProfile());

        var status = context.GetStatus();

        Assert.Equal("Profile loaded", status.Message);
        Assert.True(status.CanRunProfileActions);
    }

    [Fact]
    public void DiscoverInstalledInstance_ReturnsInstallForInstalledTrayPath()
    {
        using var workspace = InstalledTrayWorkspace.Create();
        var context = new TrayProfileContext(null);

        var install = context.DiscoverInstalledInstance(workspace.InstallationService, workspace.TrayExecutablePath);

        Assert.NotNull(install);
        Assert.Equal(workspace.TrayExecutablePath, install!.Paths.TrayExecutablePath);
    }

    private sealed class TrayProfileTestWorkspace : IDisposable
    {
        private TrayProfileTestWorkspace(string directoryPath, string commandPath)
        {
            DirectoryPath = directoryPath;
            CommandPath = commandPath;
        }

        public string DirectoryPath { get; }

        public string CommandPath { get; }

        public static TrayProfileTestWorkspace Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var commandPath = Path.Combine(directoryPath, "GridPilotHost.exe");
            File.WriteAllText(commandPath, string.Empty);
            return new TrayProfileTestWorkspace(directoryPath, commandPath);
        }

        public string WriteProfile()
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
                        args = Array.Empty<string>(),
                        workingDirectory = DirectoryPath,
                        env = new Dictionary<string, string?>()
                    },
                    logs = new
                    {
                        path = (string?)null,
                        stdoutPolicy = "jsonRpcOnly"
                    }
                });
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

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previous);
        }
    }

    private sealed class InstalledTrayWorkspace : IDisposable
    {
        private readonly string _root;

        private InstalledTrayWorkspace(string root, string trayExecutablePath, string defaultProfilePath, InstallationService installationService)
        {
            _root = root;
            TrayExecutablePath = trayExecutablePath;
            DefaultProfilePath = defaultProfilePath;
            InstallationService = installationService;
        }

        public string TrayExecutablePath { get; }

        public string DefaultProfilePath { get; }

        public InstallationService InstallationService { get; }

        public static InstalledTrayWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            var resolver = new TestInstallationPathResolver(root);
            var startup = new StartupRegistrationService(new RecordingRegistryValueStore());
            var stateService = new InstallationStateService(startup, resolver);
            var service = new InstallationService(startup, stateService, new RecordingShortcutWriter(), resolver);
            var paths = resolver.Resolve(InstallScope.PerUser);

            Directory.CreateDirectory(paths.InstallRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.HostExecutablePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.ProxyExecutablePath)!);
            File.WriteAllText(paths.TrayExecutablePath, string.Empty);
            File.WriteAllText(paths.SetupExecutablePath, string.Empty);
            File.WriteAllText(paths.HostExecutablePath, string.Empty);
            File.WriteAllText(paths.ProxyExecutablePath, string.Empty);
            stateService.WriteMetadata(InstallScope.PerUser, "v1.0.0", paths);

            return new InstalledTrayWorkspace(root, paths.TrayExecutablePath, paths.DefaultProfilePath, service);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private sealed class TestInstallationPathResolver(string root) : IInstallationPathResolver
        {
            public InstallationPaths Resolve(InstallScope scope)
            {
                var scopedRoot = Path.Combine(root, scope == InstallScope.MachineWide ? "machine" : "user");
                var installRoot = Path.Combine(scopedRoot, "app");
                return new InstallationPaths(
                    installRoot,
                    Path.Combine(scopedRoot, "profiles"),
                    Path.Combine(scopedRoot, "logs"),
                    Path.Combine(scopedRoot, "start-menu"),
                    Path.Combine(installRoot, "GridPilot.Tray.exe"),
                    Path.Combine(installRoot, "GridPilot.Setup.exe"),
                    Path.Combine(installRoot, "host", "ExcelMcp.ToolHost.exe"),
                    Path.Combine(installRoot, "proxy", "ExcelMcp.ToolProxy.exe"),
                    Path.Combine(scopedRoot, "profiles", "gridpilot-default.json"),
                    Path.Combine(scopedRoot, "install-state.json"));
            }
        }

        private sealed class RecordingRegistryValueStore : IRegistryValueStore
        {
            public string? GetValue(bool machineWide, string subKey, string name) => null;

            public void SetValue(bool machineWide, string subKey, string name, string value)
            {
            }

            public void DeleteValue(bool machineWide, string subKey, string name)
            {
            }
        }

        private sealed class RecordingShortcutWriter : IStartMenuShortcutWriter
        {
            public void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description)
            {
            }

            public void DeleteShortcut(string shortcutPath)
            {
            }
        }
    }
}
