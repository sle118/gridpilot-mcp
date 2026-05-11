namespace ExcelMcp.Deployment.Profiles;

public sealed record LaunchProfileLoadResult(
    LaunchProfile? Profile,
    IReadOnlyList<LaunchProfileIssue> Issues)
{
    public bool IsSuccess => Profile is not null && Issues.All(issue => issue.Severity != LaunchProfileIssueSeverity.Error);
}

