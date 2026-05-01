namespace ExcelMcp.Core.Logging;

public sealed class GridPilotNullLogger : IGridPilotLogger
{
    public static GridPilotNullLogger Instance { get; } = new();

    private GridPilotNullLogger()
    {
    }

    public GridPilotLogLevel Level => GridPilotLogLevel.Off;

    public bool IsEnabled(GridPilotLogLevel level) => false;

    public void Log(
        GridPilotLogLevel level,
        string category,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null,
        Exception? exception = null)
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
