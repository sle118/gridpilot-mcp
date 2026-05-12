using ExcelMcp.Deployment.Installation;
using ExcelMcp.Deployment.Profiles;

namespace GridPilot.Tray;

internal sealed record TrayProfileContext(
    string? ProfilePath,
    bool StartupLaunch = false,
    bool SuppressDashboard = false,
    bool OpenDashboardOnLaunch = false)
{
    private const string ProfilePathEnvironmentVariable = "GRIDPILOT_PROFILE_PATH";

    public bool HasProfilePath => !string.IsNullOrWhiteSpace(ProfilePath);

    public static TrayProfileContext Resolve(
        IReadOnlyList<string> args,
        string? executablePath = null,
        InstallationService? installationService = null,
        ProfileBootstrapService? profileBootstrapService = null)
    {
        var startupLaunch = false;
        var suppressDashboard = false;
        var openDashboard = false;
        string? cliProfilePath = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--profile", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Count &&
                !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                cliProfilePath = args[index + 1];
                index++;
                continue;
            }

            if (string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase))
            {
                startupLaunch = true;
                continue;
            }

            if (string.Equals(argument, "--no-dashboard", StringComparison.OrdinalIgnoreCase))
            {
                suppressDashboard = true;
                continue;
            }

            if (string.Equals(argument, "--open-dashboard", StringComparison.OrdinalIgnoreCase))
            {
                openDashboard = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(cliProfilePath))
        {
            return new TrayProfileContext(cliProfilePath, startupLaunch, suppressDashboard, openDashboard);
        }

        var envPath = Environment.GetEnvironmentVariable(ProfilePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return new TrayProfileContext(envPath, startupLaunch, suppressDashboard, openDashboard);
        }

        var installedProfilePath = ResolveInstalledProfilePath(
            executablePath ?? Environment.ProcessPath,
            installationService,
            profileBootstrapService);
        return new TrayProfileContext(installedProfilePath, startupLaunch, suppressDashboard, openDashboard);
    }

    public LaunchProfileLoadResult LoadProfile() =>
        HasProfilePath
            ? LaunchProfileLoader.Load(ProfilePath!)
            : new LaunchProfileLoadResult(
                null,
                [
                    new LaunchProfileIssue(
                        LaunchProfileIssueSeverity.Error,
                        "profile_not_configured",
                        "No launch profile is configured. Start the tray with --profile <path> or set GRIDPILOT_PROFILE_PATH.",
                        "$")
                ]);

    public TrayProfileStatus GetStatus()
    {
        if (!HasProfilePath)
        {
            return new TrayProfileStatus("No profile configured", CanRunProfileActions: false);
        }

        var load = LoadProfile();
        if (load.Profile is null)
        {
            return new TrayProfileStatus("Profile invalid or missing", CanRunProfileActions: false);
        }

        var validation = LaunchProfileValidator.Validate(load.Profile);
        return validation.IsValid
            ? new TrayProfileStatus("Profile loaded", CanRunProfileActions: true)
            : new TrayProfileStatus("Profile validation failed", CanRunProfileActions: false);
    }

    public static InstalledInstanceState? DiscoverInstalledInstance(
        string? executablePath = null,
        InstallationService? installationService = null)
    {
        var resolvedExecutablePath = executablePath ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(resolvedExecutablePath))
        {
            return null;
        }

        installationService ??= new InstallationService();
        return installationService.DiscoverByExecutablePath(resolvedExecutablePath);
    }

    public InstalledInstanceState? DiscoverInstalledInstance(
        InstallationService? installationService = null,
        string? executablePath = null) =>
        DiscoverInstalledInstance(executablePath, installationService);

    private static string? ResolveInstalledProfilePath(
        string? executablePath,
        InstallationService? installationService,
        ProfileBootstrapService? profileBootstrapService)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        installationService ??= new InstallationService();
        profileBootstrapService ??= new ProfileBootstrapService();

        var install = DiscoverInstalledInstance(executablePath, installationService);
        return install is null
            ? null
            : profileBootstrapService.EnsureDefaultProfile(install);
    }
}
