namespace ExcelMcp.Core;

public sealed record RefreshOptions(
    bool Silent = false,
    bool PreferSynchronousTableRefresh = true,
    TimeSpan? Timeout = null);
