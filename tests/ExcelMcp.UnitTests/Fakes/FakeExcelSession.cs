using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;

namespace ExcelMcp.UnitTests.Fakes;

internal sealed class FakeExcelSession : IExcelSession
{
    public required IWorkbookHandle Workbook { get; init; }
    public List<SessionOptions> PushedOptions { get; } = [];
    public int PopCallCount { get; private set; }
    public List<TimeSpan> WaitCalls { get; } = [];
    public IReadOnlyList<WorkbookSummary> OpenWorkbooks { get; set; } = Array.Empty<WorkbookSummary>();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<SessionState> GetStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SessionState(true, true, true, true, false));

    public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OpenWorkbooks);

    public Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(Workbook);

    public Task<ScopedSessionToken> PushOptionsAsync(SessionOptions options, CancellationToken cancellationToken = default)
    {
        PushedOptions.Add(options);
        return Task.FromResult(ScopedSessionToken.New());
    }

    public Task PopOptionsAsync(ScopedSessionToken token, CancellationToken cancellationToken = default)
    {
        PopCallCount++;
        return Task.CompletedTask;
    }

    public Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        WaitCalls.Add(timeout);
        return Task.CompletedTask;
    }
}
