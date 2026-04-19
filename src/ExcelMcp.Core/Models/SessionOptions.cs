namespace ExcelMcp.Core;

public sealed record SessionOptions(
    bool? DisplayAlerts = null,
    bool? ScreenUpdating = null,
    bool? EnableEvents = null,
    bool? Visible = null,
    bool? FastCombine = null);
