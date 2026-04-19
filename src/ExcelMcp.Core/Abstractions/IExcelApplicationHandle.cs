namespace ExcelMcp.Core.Abstractions;

public interface IExcelApplicationHandle : IAsyncDisposable
{
    SessionState CaptureState();
    void ApplyOptions(SessionOptions options);
    void RestoreState(SessionState state);

    Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default);
    Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
