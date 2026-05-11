namespace ExcelMcp.Deployment.Profiles;

public sealed record LaunchProfileIssue(
    LaunchProfileIssueSeverity Severity,
    string Code,
    string Message,
    string? Path = null);

