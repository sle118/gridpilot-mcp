namespace ExcelMcp.Core.Logging;

public static class GridPilotLoggerFactory
{
    public static GridPilotMutableLogger Create(GridPilotLogLevel level, string? logPath) =>
        new(level, logPath);
}
