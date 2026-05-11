using ExcelMcp.Deployment.Profiles;

namespace GridPilot.Tray;

internal sealed record TrayProfileContext(string? ProfilePath)
{
    private const string ProfilePathEnvironmentVariable = "GRIDPILOT_PROFILE_PATH";

    public bool HasProfilePath => !string.IsNullOrWhiteSpace(ProfilePath);

    public static TrayProfileContext Resolve(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--profile", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Count &&
                !string.IsNullOrWhiteSpace(args[index + 1]))
            {
                return new TrayProfileContext(args[index + 1]);
            }
        }

        var envPath = Environment.GetEnvironmentVariable(ProfilePathEnvironmentVariable);
        return new TrayProfileContext(string.IsNullOrWhiteSpace(envPath) ? null : envPath);
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
}
