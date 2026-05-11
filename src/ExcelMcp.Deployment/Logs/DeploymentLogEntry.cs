namespace ExcelMcp.Deployment.Logs;

public sealed record DeploymentLogEntry(
    DeploymentLogKind Kind,
    string Path,
    bool Exists,
    long? SizeBytes,
    DateTimeOffset? LastWriteTimeUtc,
    DeploymentLogAccessStatus AccessStatus,
    string? Message = null);
