namespace ExcelMcp.Core.Logging;

public interface IGridPilotLogger : IAsyncDisposable
{
    GridPilotLogLevel Level { get; }

    bool IsEnabled(GridPilotLogLevel level);

    void Log(
        GridPilotLogLevel level,
        string category,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null,
        Exception? exception = null);
}
