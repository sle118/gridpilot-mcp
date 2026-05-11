namespace ExcelMcp.Deployment.Profiles;

public sealed record LaunchProfileValidationResult(IReadOnlyList<LaunchProfileIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity != LaunchProfileIssueSeverity.Error);
}

