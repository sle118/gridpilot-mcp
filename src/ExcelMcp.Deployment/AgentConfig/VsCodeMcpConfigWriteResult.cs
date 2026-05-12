namespace ExcelMcp.Deployment.AgentConfig;

public sealed record VsCodeMcpConfigWriteResult(
    string ConfigPath,
    string? BackupPath,
    VsCodeMcpConfigWriteAction Action,
    bool WasWritten,
    string Diff,
    IReadOnlyList<string> SummaryLines,
    IReadOnlyList<AgentConfigIssue> Issues)
{
    public bool IsSuccess => Action != VsCodeMcpConfigWriteAction.Failed;
}
