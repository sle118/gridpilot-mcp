using System.Text.Json;
using ExcelMcp.Deployment.Installation;

namespace ExcelMcp.UnitTests.Deployment.Installation;

internal sealed class InstallationTestWorkspace : IDisposable
{
    private readonly string _root;
    private readonly TestInstallationPathResolver _pathResolver;

    private InstallationTestWorkspace(string root)
    {
        _root = root;
        ReleaseRoot = Path.Combine(root, "release");
        Directory.CreateDirectory(ReleaseRoot);
        RegistryStore = new RecordingRegistryValueStore();
        ShortcutWriter = new RecordingShortcutWriter();
        _pathResolver = new TestInstallationPathResolver(root);
        WritePayload("v1.0.0");
    }

    public string ReleaseRoot { get; }

    public RecordingRegistryValueStore RegistryStore { get; }

    public RecordingShortcutWriter ShortcutWriter { get; }

    public static InstallationTestWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new InstallationTestWorkspace(root);
    }

    public InstallationService CreateInstallationService(StartupRegistrationService? startupRegistrationService = null)
    {
        startupRegistrationService ??= new StartupRegistrationService(RegistryStore);
        var stateService = new InstallationStateService(startupRegistrationService, _pathResolver);
        return new InstallationService(startupRegistrationService, stateService, ShortcutWriter, _pathResolver);
    }

    public InstalledInstanceState CreateInstalledInstanceState(InstallScope scope, string version)
    {
        var paths = _pathResolver.Resolve(scope);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.HostExecutablePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ProxyExecutablePath)!);
        File.WriteAllText(paths.TrayExecutablePath, string.Empty);
        File.WriteAllText(paths.SetupExecutablePath, string.Empty);
        File.WriteAllText(paths.HostExecutablePath, string.Empty);
        File.WriteAllText(paths.ProxyExecutablePath, string.Empty);
        return new InstalledInstanceState(scope, version, paths, StartupEnabled: false, InstalledAtUtc: DateTimeOffset.UtcNow);
    }

    public void SetReleaseVersion(string version) => WritePayload(version);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WritePayload(string version)
    {
        Directory.CreateDirectory(Path.Combine(ReleaseRoot, "host"));
        Directory.CreateDirectory(Path.Combine(ReleaseRoot, "proxy"));
        File.WriteAllText(Path.Combine(ReleaseRoot, "GridPilot.Tray.exe"), "tray");
        File.WriteAllText(Path.Combine(ReleaseRoot, "GridPilot.Setup.exe"), "setup");
        File.WriteAllText(Path.Combine(ReleaseRoot, "host", "ExcelMcp.ToolHost.exe"), "host");
        File.WriteAllText(Path.Combine(ReleaseRoot, "proxy", "ExcelMcp.ToolProxy.exe"), "proxy");
        File.WriteAllText(
            Path.Combine(ReleaseRoot, "release-manifest.json"),
            JsonSerializer.Serialize(new
            {
                version,
                files = new[] { "GridPilot.Tray.exe", "GridPilot.Setup.exe", "host/", "proxy/" }
            }));
    }

    internal sealed class TestInstallationPathResolver(string root) : IInstallationPathResolver
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

    internal sealed class RecordingRegistryValueStore : IRegistryValueStore
    {
        private readonly Dictionary<(bool MachineWide, string SubKey, string Name), string> _values = [];

        public string? GetValue(bool machineWide, string subKey, string name) =>
            _values.TryGetValue((machineWide, subKey, name), out var value) ? value : null;

        public void SetValue(bool machineWide, string subKey, string name, string value) =>
            _values[(machineWide, subKey, name)] = value;

        public void DeleteValue(bool machineWide, string subKey, string name) =>
            _values.Remove((machineWide, subKey, name));
    }

    internal sealed class RecordingShortcutWriter : IStartMenuShortcutWriter
    {
        public List<string> CreatedShortcuts { get; } = [];

        public void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description) =>
            CreatedShortcuts.Add($"{shortcutPath}|{targetPath}|{arguments}|{description}");

        public void DeleteShortcut(string shortcutPath) =>
            CreatedShortcuts.RemoveAll(item => item.StartsWith(shortcutPath, StringComparison.OrdinalIgnoreCase));
    }
}
