namespace ExcelMcp.ToolHost.Diagnostics;

public sealed record ToolTargetContext(
    string? ConnectionId,
    string? WorkbookPath,
    string? WorkbookName,
    string? TargetResolutionMode);

public sealed record ToolWorkflowGuidance(
    ToolTargetContext? TargetContext,
    IReadOnlyList<string> RecommendedNextTools,
    IReadOnlyList<string> WorkflowHints);

public sealed record ToolRemediationHint(
    string HintCode,
    string Message,
    string? RecommendedTool = null,
    object? SuggestedArguments = null);
