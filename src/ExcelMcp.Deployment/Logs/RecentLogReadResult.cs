namespace ExcelMcp.Deployment.Logs;

public sealed record RecentLogReadResult(
    string Path,
    bool Exists,
    DeploymentLogAccessStatus AccessStatus,
    IReadOnlyList<string> Lines,
    bool WasTruncated,
    string? Message = null)
{
    public bool IsSuccess => AccessStatus == DeploymentLogAccessStatus.Accessible;
}
