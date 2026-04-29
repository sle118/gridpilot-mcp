using ExcelMcp.Core.Abstractions;

namespace ExcelMcp.IntegrationTests.Fakes;

internal sealed class FakeExcelSession : IExcelSession
{
    public required IWorkbookHandle Workbook { get; init; }
    public IReadOnlyList<WorkbookSummary> OpenWorkbooks { get; set; } = Array.Empty<WorkbookSummary>();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<SessionState> GetStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionState(true, true, true, true, false));

    public Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Workbook);

    public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OpenWorkbooks);

    public Task<ScopedSessionToken> PushOptionsAsync(SessionOptions options, CancellationToken cancellationToken = default) =>
        Task.FromResult(ScopedSessionToken.New());

    public Task PopOptionsAsync(ScopedSessionToken token, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
