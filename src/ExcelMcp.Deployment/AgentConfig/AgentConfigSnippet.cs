namespace ExcelMcp.Deployment.AgentConfig;

public sealed record AgentConfigSnippet(
    AgentTarget Target,
    string DisplayName,
    string SuggestedFileName,
    string Language,
    string Content,
    IReadOnlyList<AgentConfigIssue> Issues)
{
    public bool IsSuccess => Content.Length > 0 && Issues.All(issue => issue.Severity != AgentConfigIssueSeverity.Error);
}

