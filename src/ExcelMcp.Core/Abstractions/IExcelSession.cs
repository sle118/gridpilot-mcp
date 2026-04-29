namespace ExcelMcp.Core.Abstractions;

public interface IExcelSession : IAsyncDisposable
{
    Task<SessionState> GetStateAsync(CancellationToken cancellationToken = default);
    Task<SessionDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
    Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default);
    async Task<SessionOptionsScope> BeginScopeAsync(SessionOptions options, CancellationToken cancellationToken = default)
    {
        var token = await PushOptionsAsync(options, cancellationToken).ConfigureAwait(false);
        return new SessionOptionsScope(this, token);
    }

    Task<ScopedSessionToken> PushOptionsAsync(SessionOptions options, CancellationToken cancellationToken = default);
    Task PopOptionsAsync(ScopedSessionToken token, CancellationToken cancellationToken = default);
    Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
