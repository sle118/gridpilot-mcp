using System.Text;
using ExcelMcp.Deployment.Profiles;

namespace GridPilot.Tray;

internal static class ProfileOverviewPresenter
{
    public static ProfileOverviewState Create(TrayProfileContext profileContext)
    {
        if (!profileContext.HasProfilePath)
        {
            const string message = "No profile configured";
            return new ProfileOverviewState(
                ProfilePath: null,
                Status: message,
                Details: "Start the tray with --profile <path> or set GRIDPILOT_PROFILE_PATH.",
                CanRunProfileActions: false,
                Profile: null);
        }

        var load = profileContext.LoadProfile();
        if (load.Profile is null)
        {
            return new ProfileOverviewState(
                profileContext.ProfilePath,
                "Profile invalid or missing",
                FormatProfileIssues(load.Issues),
                CanRunProfileActions: false,
                Profile: null);
        }

        var validation = LaunchProfileValidator.Validate(load.Profile);
        if (!validation.IsValid)
        {
            return new ProfileOverviewState(
                profileContext.ProfilePath,
                "Profile validation failed",
                FormatProfileIssues(validation.Issues),
                CanRunProfileActions: false,
                load.Profile);
        }

        return new ProfileOverviewState(
            profileContext.ProfilePath,
            "Profile loaded",
            FormatProfile(load.Profile),
            CanRunProfileActions: true,
            load.Profile);
    }

    public static string FormatProfile(LaunchProfile profile)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Name: {ValueOrMissing(profile.Name)}");
        builder.AppendLine($"Display name: {ValueOrMissing(profile.DisplayName)}");
        builder.AppendLine($"Description: {ValueOrMissing(profile.Metadata?.Description)}");
        builder.AppendLine($"Host command: {ValueOrMissing(profile.Host?.Command)}");
        builder.AppendLine($"Args: {FormatArgs(profile.Host?.Args)}");
        builder.AppendLine($"Working directory: {ValueOrMissing(profile.Host?.WorkingDirectory)}");
        builder.AppendLine($"Stdout policy: {ValueOrMissing(profile.Logs?.StdoutPolicy)}");
        builder.AppendLine($"Log path: {ValueOrMissing(profile.Logs?.Path)}");
        builder.AppendLine($"Env keys: {FormatEnvironmentKeys(profile.Host?.Env)}");
        return builder.ToString().TrimEnd();
    }

    public static string FormatEnvironmentKeys(IReadOnlyDictionary<string, string?>? environment)
    {
        if (environment is null)
        {
            return "(missing)";
        }

        if (environment.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", environment.Keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    public static string FormatProfileIssues(IEnumerable<LaunchProfileIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));

    private static string FormatArgs(IReadOnlyList<string>? args) =>
        args is null ? "(missing)" : args.Count == 0 ? "(none)" : string.Join(" ", args.Select(QuoteIfNeeded));

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private static string ValueOrMissing(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(missing)" : value;
}
