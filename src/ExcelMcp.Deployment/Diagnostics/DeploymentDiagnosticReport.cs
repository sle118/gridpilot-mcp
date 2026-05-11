using ExcelMcp.Deployment.Logs;

namespace ExcelMcp.Deployment.Diagnostics;

public sealed record DeploymentDiagnosticReport(
    string Content,
    IReadOnlyList<DeploymentLogEntry> Logs,
    IReadOnlyList<RecentLogReadResult> RecentLogTails);
