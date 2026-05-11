namespace ExcelMcp.Deployment.AgentConfig;

public sealed record AgentConfigIssue(
    AgentConfigIssueSeverity Severity,
    string Code,
    string Message);

