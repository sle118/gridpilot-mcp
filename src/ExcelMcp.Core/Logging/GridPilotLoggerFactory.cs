namespace ExcelMcp.Core.Logging;

public static class GridPilotLoggerFactory
{
    public static IGridPilotLogger Create(GridPilotLogLevel level, string? logPath) =>
        level == GridPilotLogLevel.Off
            ? GridPilotNullLogger.Instance
            : new GridPilotFileLogger(logPath ?? throw new InvalidOperationException("A log path is required when runtime logging is enabled."), level);
}
