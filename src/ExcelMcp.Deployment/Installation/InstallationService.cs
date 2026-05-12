namespace ExcelMcp.Deployment.Installation;

public sealed class InstallationService
{
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly InstallationStateService _stateService;
    private readonly IStartMenuShortcutWriter _shortcutWriter;
    private readonly IInstallationPathResolver _pathResolver;

    public InstallationService(
        StartupRegistrationService? startupRegistrationService = null,
        InstallationStateService? stateService = null,
        IStartMenuShortcutWriter? shortcutWriter = null,
        IInstallationPathResolver? pathResolver = null)
    {
        _startupRegistrationService = startupRegistrationService ?? new StartupRegistrationService();
        _pathResolver = pathResolver ?? new DefaultInstallationPathResolver();
        _stateService = stateService ?? new InstallationStateService(_startupRegistrationService, _pathResolver);
        _shortcutWriter = shortcutWriter ?? new WindowsStartMenuShortcutWriter();
    }

    public InstalledInstanceState? Discover(InstallScope scope) => _stateService.Discover(scope);

    public IReadOnlyList<InstalledInstanceState> DiscoverAll() => _stateService.DiscoverAll();

    public InstalledInstanceState? DiscoverByExecutablePath(string executablePath) => _stateService.DiscoverByExecutablePath(executablePath);

    public SetupPlan BuildPlan(SetupOptions options, bool isElevated)
    {
        ArgumentNullException.ThrowIfNull(options);

        var payload = ReleasePayloadReader.Read(options.SourceRoot);
        var paths = _pathResolver.Resolve(options.Scope);
        var existing = Discover(options.Scope);
        var operation = existing is null
            ? SetupOperationKind.Install
            : string.Equals(existing.Version, payload.Version, StringComparison.OrdinalIgnoreCase)
                ? SetupOperationKind.Repair
                : SetupOperationKind.Update;
        var requiresElevation = SetupElevationPolicy.RequiresElevation(options.Scope, isElevated);
        var previewLines = BuildInstallPreview(operation, options, payload.Version, paths, existing, requiresElevation);

        return new SetupPlan(operation, options, payload.Version, paths, existing, requiresElevation, previewLines);
    }

    public IReadOnlyList<string> BuildUninstallPreview(InstallScope scope)
    {
        var paths = _pathResolver.Resolve(scope);
        var lines = new List<string>
        {
            $"Remove installed binaries from {paths.InstallRoot}",
            $"Remove startup registration from {(scope == InstallScope.MachineWide ? "HKLM" : "HKCU")} Run",
            $"Remove start menu shortcuts from {paths.StartMenuProgramsRoot}",
            $"Remove install metadata from {paths.MetadataPath}",
            $"Preserve user profiles in {paths.ProfileRoot}",
            $"Preserve logs in {paths.LogRoot}"
        };
        return lines;
    }

    public async Task<InstalledInstanceState> ApplyPlanAsync(SetupPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var payload = ReleasePayloadReader.Read(plan.Options.SourceRoot);
        Directory.CreateDirectory(plan.Paths.InstallRoot);
        CopyPayload(payload.SourceRoot, plan.Paths.InstallRoot, cancellationToken);

        if (plan.Options.CreateStartMenuShortcut)
        {
            var shortcutPath = Path.Combine(plan.Paths.StartMenuProgramsRoot, InstallationPathsResolver.StartMenuShortcutFileName);
            _shortcutWriter.CreateShortcut(
                shortcutPath,
                plan.Paths.TrayExecutablePath,
                "--open-dashboard",
                "Launch GridPilot MCP");
        }
        else
        {
            _shortcutWriter.DeleteShortcut(Path.Combine(plan.Paths.StartMenuProgramsRoot, InstallationPathsResolver.StartMenuShortcutFileName));
        }

        if (plan.Options.StartupEnabled)
        {
            _startupRegistrationService.Enable(new StartupRegistrationOptions(
                plan.Options.Scope,
                plan.Paths.TrayExecutablePath,
                ["--startup", "--no-dashboard"]));
        }
        else
        {
            _startupRegistrationService.Disable(plan.Options.Scope);
        }

        _stateService.WriteMetadata(plan.Options.Scope, payload.Version, plan.Paths);

        await Task.CompletedTask;
        return _stateService.Discover(plan.Options.Scope) ??
            throw new InvalidOperationException("Installation metadata was not written successfully.");
    }

    public async Task UninstallAsync(InstallScope scope, CancellationToken cancellationToken = default)
    {
        var paths = _pathResolver.Resolve(scope);
        _startupRegistrationService.Disable(scope);
        _shortcutWriter.DeleteShortcut(Path.Combine(paths.StartMenuProgramsRoot, InstallationPathsResolver.StartMenuShortcutFileName));

        if (Directory.Exists(paths.InstallRoot))
        {
            Directory.Delete(paths.InstallRoot, recursive: true);
        }

        if (Directory.Exists(paths.StartMenuProgramsRoot) &&
            !Directory.EnumerateFileSystemEntries(paths.StartMenuProgramsRoot).Any())
        {
            Directory.Delete(paths.StartMenuProgramsRoot, recursive: false);
        }

        _stateService.DeleteMetadata(scope);
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static IReadOnlyList<string> BuildInstallPreview(
        SetupOperationKind operation,
        SetupOptions options,
        string version,
        InstallationPaths paths,
        InstalledInstanceState? existing,
        bool requiresElevation)
    {
        var lines = new List<string>
        {
            $"{operation} GridPilot MCP {version}",
            $"Source payload: {options.SourceRoot}",
            $"Install scope: {options.Scope}",
            $"Install root: {paths.InstallRoot}",
            $"Default profile path: {paths.DefaultProfilePath}",
            $"Log root: {paths.LogRoot}",
            options.StartupEnabled
                ? $"Enable startup registration in {(options.Scope == InstallScope.MachineWide ? "HKLM" : "HKCU")} Run"
                : "Do not register Windows startup",
            options.CreateStartMenuShortcut
                ? $"Create start menu shortcut in {paths.StartMenuProgramsRoot}"
                : "Do not create a start menu shortcut"
        };

        if (existing is not null)
        {
            lines.Add($"Existing install version: {existing.Version}");
        }

        if (requiresElevation)
        {
            lines.Add("This operation requires elevation and will relaunch the setup app as administrator.");
        }

        return lines;
    }

    private static void CopyPayload(string sourceRoot, string destinationRoot, CancellationToken cancellationToken)
    {
        var sourceFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
        var sourceDirectories = Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .ToArray();
        var sourceFileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceDirectorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceDirectory in sourceDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeDirectory = Path.GetRelativePath(sourceRoot, sourceDirectory);
            sourceDirectorySet.Add(relativeDirectory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relativeDirectory));
        }

        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeFile = Path.GetRelativePath(sourceRoot, sourceFile);
            sourceFileSet.Add(relativeFile);
            var destinationFile = Path.Combine(destinationRoot, relativeFile);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile) ?? destinationRoot);

            if (string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(destinationFile), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(sourceFile, destinationFile, overwrite: true);
        }

        if (!Directory.Exists(destinationRoot))
        {
            return;
        }

        foreach (var destinationFile in Directory.GetFiles(destinationRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeFile = Path.GetRelativePath(destinationRoot, destinationFile);
            if (!sourceFileSet.Contains(relativeFile))
            {
                File.Delete(destinationFile);
            }
        }

        foreach (var destinationDirectory in Directory.GetDirectories(destinationRoot, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeDirectory = Path.GetRelativePath(destinationRoot, destinationDirectory);
            if (!sourceDirectorySet.Contains(relativeDirectory) &&
                !Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
            {
                Directory.Delete(destinationDirectory, recursive: false);
            }
        }
    }
}
