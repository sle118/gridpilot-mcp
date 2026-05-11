using ExcelMcp.Deployment.Logs;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.Deployment.Diagnostics;

public static class DeploymentDiagnosticReportBuilder
{
    private static readonly string[] SensitiveEnvironmentKeyParts =
    [
        "TOKEN",
        "SECRET",
        "KEY",
        "PASSWORD",
        "CREDENTIAL"
    ];

    public static async Task<DeploymentDiagnosticReport> BuildAsync(
        LaunchProfile profile,
        DeploymentDiagnosticReportOptions? options = null,
        string? currentDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        options ??= new DeploymentDiagnosticReportOptions();
        var logs = DeploymentLogLocator.Locate(profile, currentDirectory);
        var tails = new List<RecentLogReadResult>();
        if (options.IncludeRecentLogTails)
        {
            foreach (var log in logs)
            {
                tails.Add(await RecentLogReader.ReadTailAsync(log.Path, options.RecentLogOptions, cancellationToken)
                    .ConfigureAwait(false));
            }
        }

        return new DeploymentDiagnosticReport(BuildContent(profile, logs, tails, options.IncludeRecentLogTails), logs, tails);
    }

    private static string BuildContent(
        LaunchProfile profile,
        IReadOnlyList<DeploymentLogEntry> logs,
        IReadOnlyList<RecentLogReadResult> tails,
        bool includeRecentLogTails)
    {
        var lines = new List<string>
        {
            "# GridPilot Deployment Diagnostic Report",
            string.Empty,
            "## Profile",
            $"- Name: {ValueOrMissing(profile.Name)}",
            $"- Display name: {ValueOrMissing(profile.DisplayName)}",
            $"- Description: {ValueOrMissing(profile.Metadata?.Description)}",
            string.Empty,
            "## Host",
            $"- Command: {ValueOrMissing(profile.Host?.Command)}",
            $"- Args: {FormatArgs(profile.Host?.Args)}",
            $"- Working directory: {ValueOrMissing(profile.Host?.WorkingDirectory)}",
            $"- Stdout policy: {ValueOrMissing(profile.Logs?.StdoutPolicy)}",
            string.Empty,
            "## Environment"
        };

        AppendEnvironment(lines, profile.Host?.Env);

        lines.Add(string.Empty);
        lines.Add("## Logs");
        foreach (var log in logs)
        {
            lines.Add($"- {FormatKind(log.Kind)}: {log.Path}");
            lines.Add($"  Exists: {log.Exists.ToString().ToLowerInvariant()}");
            lines.Add($"  Access: {log.AccessStatus}");
            lines.Add($"  Size bytes: {log.SizeBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(unknown)"}");
            lines.Add($"  Last write UTC: {log.LastWriteTimeUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "(unknown)"}");
            if (!string.IsNullOrWhiteSpace(log.Message))
            {
                lines.Add($"  Message: {log.Message}");
            }
        }

        if (includeRecentLogTails)
        {
            lines.Add(string.Empty);
            lines.Add("## Recent Log Tails");
            foreach (var tail in tails)
            {
                lines.Add($"### {tail.Path}");
                lines.Add($"- Status: {tail.AccessStatus}");
                lines.Add($"- Lines: {tail.Lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                lines.Add($"- Truncated: {tail.WasTruncated.ToString().ToLowerInvariant()}");
                if (!string.IsNullOrWhiteSpace(tail.Message))
                {
                    lines.Add($"- Message: {tail.Message}");
                }

                if (tail.Lines.Count > 0)
                {
                    lines.Add("```text");
                    lines.AddRange(tail.Lines);
                    lines.Add("```");
                }
            }
        }
        else
        {
            lines.Add(string.Empty);
            lines.Add("Recent log tails were not included.");
        }

        return string.Join("\n", lines);
    }

    private static void AppendEnvironment(List<string> lines, IReadOnlyDictionary<string, string?>? environment)
    {
        if (environment is null || environment.Count == 0)
        {
            lines.Add("- (empty)");
            return;
        }

        foreach (var (key, value) in environment.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            lines.Add($"- {key}: {FormatEnvironmentValue(key, value)}");
        }
    }

    private static string FormatEnvironmentValue(string key, string? value) =>
        IsSensitiveEnvironmentKey(key) ? "<redacted>" : ValueOrMissing(value);

    private static bool IsSensitiveEnvironmentKey(string key) =>
        SensitiveEnvironmentKeyParts.Any(part => key.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static string FormatArgs(IReadOnlyList<string>? args) =>
        args is null ? "(missing)" : string.Join(" ", args.Select(QuoteIfNeeded));

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private static string FormatKind(DeploymentLogKind kind) =>
        kind switch
        {
            DeploymentLogKind.ProfileConfigured => "profile-configured",
            DeploymentLogKind.HostEnvironment => "host-env",
            DeploymentLogKind.HostConventional => "host-conventional",
            DeploymentLogKind.ProxyConventional => "proxy-conventional",
            _ => kind.ToString()
        };

    private static string ValueOrMissing(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(missing)" : value;
}
