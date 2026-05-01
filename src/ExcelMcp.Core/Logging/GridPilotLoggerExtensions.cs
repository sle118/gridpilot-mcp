namespace ExcelMcp.Core.Logging;

public static class GridPilotLoggerExtensions
{
    public static void LogInfo(
        this IGridPilotLogger logger,
        string category,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null,
        Exception? exception = null) =>
        logger.Log(GridPilotLogLevel.Info, category, eventName, fields, exception);

    public static void LogDebug(
        this IGridPilotLogger logger,
        string category,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null,
        Exception? exception = null) =>
        logger.Log(GridPilotLogLevel.Debug, category, eventName, fields, exception);

    public static void LogTrace(
        this IGridPilotLogger logger,
        string category,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null,
        Exception? exception = null) =>
        logger.Log(GridPilotLogLevel.Trace, category, eventName, fields, exception);
}
