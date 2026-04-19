namespace ExcelMcp.Core;

public sealed record SessionState(
    bool DisplayAlerts,
    bool ScreenUpdating,
    bool EnableEvents,
    bool Visible,
    bool? FastCombine);
