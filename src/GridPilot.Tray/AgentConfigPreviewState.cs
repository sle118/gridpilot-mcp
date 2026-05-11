namespace GridPilot.Tray;

internal sealed record AgentConfigPreviewState(
    string DisplayName,
    string SuggestedFileName,
    string Language,
    string Content,
    string IssuesText,
    bool CanCopy);
