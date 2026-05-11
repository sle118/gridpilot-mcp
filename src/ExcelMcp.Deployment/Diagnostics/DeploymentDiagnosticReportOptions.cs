using ExcelMcp.Deployment.Logs;

namespace ExcelMcp.Deployment.Diagnostics;

public sealed record DeploymentDiagnosticReportOptions
{
    public bool IncludeRecentLogTails { get; init; }

    public RecentLogReadOptions RecentLogOptions { get; init; } = RecentLogReadOptions.Default;
}
