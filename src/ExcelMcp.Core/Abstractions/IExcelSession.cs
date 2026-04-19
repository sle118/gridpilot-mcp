namespace ExcelMcp.Core.Abstractions;

public interface IExcelSession : IAsyncDisposable
{
    Task<SessionState> GetStateAsync(CancellationToken cancellationToken = default);
    Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default);
    Task<ScopedSessionToken> PushOptionsAsync(SessionOptions options, CancellationToken cancellationToken = default);
    Task PopOptionsAsync(ScopedSessionToken token, CancellationToken cancellationToken = default);
    Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
