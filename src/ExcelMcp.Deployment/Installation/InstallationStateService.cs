using System.Text.Json;

namespace ExcelMcp.Deployment.Installation;

public sealed class InstallationStateService
{
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly IInstallationPathResolver _pathResolver;

    public InstallationStateService(
        StartupRegistrationService? startupRegistrationService = null,
        IInstallationPathResolver? pathResolver = null)
    {
        _startupRegistrationService = startupRegistrationService ?? new StartupRegistrationService();
        _pathResolver = pathResolver ?? new DefaultInstallationPathResolver();
    }

    public InstalledInstanceState? Discover(InstallScope scope)
    {
        var paths = _pathResolver.Resolve(scope);
        if (!File.Exists(paths.MetadataPath))
        {
            return null;
        }

        var metadata = JsonSerializer.Deserialize<InstallationMetadata>(File.ReadAllText(paths.MetadataPath));
        if (metadata is null)
        {
            return null;
        }

        var resolvedPaths = string.IsNullOrWhiteSpace(metadata.InstallRoot)
            ? paths
            : new InstallationPaths(
                metadata.InstallRoot,
                metadata.ProfileRoot ?? paths.ProfileRoot,
                metadata.LogRoot ?? paths.LogRoot,
                metadata.StartMenuProgramsRoot ?? paths.StartMenuProgramsRoot,
                metadata.TrayExecutablePath ?? paths.TrayExecutablePath,
                metadata.SetupExecutablePath ?? paths.SetupExecutablePath,
                metadata.HostExecutablePath ?? paths.HostExecutablePath,
                metadata.ProxyExecutablePath ?? paths.ProxyExecutablePath,
                metadata.DefaultProfilePath ?? paths.DefaultProfilePath,
                metadata.MetadataPath ?? paths.MetadataPath);

        var startupEnabled = _startupRegistrationService.IsEnabled(new StartupRegistrationOptions(
            scope,
            resolvedPaths.TrayExecutablePath,
            ["--startup", "--no-dashboard"]));

        return new InstalledInstanceState(
            metadata.Scope,
            metadata.Version ?? "unknown",
            resolvedPaths,
            startupEnabled,
            metadata.InstalledAtUtc);
    }

    public IReadOnlyList<InstalledInstanceState> DiscoverAll()
    {
        var installs = new List<InstalledInstanceState>();
        foreach (var scope in Enum.GetValues<InstallScope>())
        {
            var state = Discover(scope);
            if (state is not null)
            {
                installs.Add(state);
            }
        }

        return installs;
    }

    public InstalledInstanceState? DiscoverByExecutablePath(string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath);
        return DiscoverAll().FirstOrDefault(state =>
            string.Equals(Path.GetFullPath(state.Paths.TrayExecutablePath), fullPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(state.Paths.SetupExecutablePath), fullPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                Path.GetFullPath(state.Paths.InstallRoot).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetDirectoryName(fullPath)?.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase));
    }

    public void WriteMetadata(InstallScope scope, string version, InstallationPaths paths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(paths.MetadataPath) ?? throw new InvalidOperationException("Metadata path is missing a parent folder."));

        var metadata = new InstallationMetadata
        {
            Scope = scope,
            Version = version,
            InstallRoot = paths.InstallRoot,
            ProfileRoot = paths.ProfileRoot,
            LogRoot = paths.LogRoot,
            StartMenuProgramsRoot = paths.StartMenuProgramsRoot,
            TrayExecutablePath = paths.TrayExecutablePath,
            SetupExecutablePath = paths.SetupExecutablePath,
            HostExecutablePath = paths.HostExecutablePath,
            ProxyExecutablePath = paths.ProxyExecutablePath,
            DefaultProfilePath = paths.DefaultProfilePath,
            MetadataPath = paths.MetadataPath,
            InstalledAtUtc = DateTimeOffset.UtcNow
        };

        File.WriteAllText(paths.MetadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    public void DeleteMetadata(InstallScope scope)
    {
        var paths = _pathResolver.Resolve(scope);
        if (File.Exists(paths.MetadataPath))
        {
            File.Delete(paths.MetadataPath);
        }
    }
}
