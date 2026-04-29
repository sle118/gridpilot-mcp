using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;

namespace ExcelMcp.UnitTests.Fakes;

internal sealed class FakeExcelApplicationHandle : IExcelApplicationHandle
{
    private readonly Stack<SessionState> _restoreHistory = new();

    public FakeExcelApplicationHandle(SessionState initialState)
    {
        CurrentState = initialState;
    }

    public SessionState CurrentState { get; private set; }
    public SessionDiagnostics CurrentDiagnostics { get; set; } = new(ExcelSessionMode.CreateNew, true, true, ExcelCalculationState.Done);

    public IReadOnlyCollection<SessionState> RestoreHistory => _restoreHistory;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public SessionState CaptureState() => CurrentState;

    public SessionDiagnostics CaptureDiagnostics() => CurrentDiagnostics;

    public void ApplyOptions(SessionOptions options)
    {
        CurrentState = CurrentState with
        {
            DisplayAlerts = options.DisplayAlerts ?? CurrentState.DisplayAlerts,
            EnableEvents = options.EnableEvents ?? CurrentState.EnableEvents,
            ScreenUpdating = options.ScreenUpdating ?? CurrentState.ScreenUpdating,
            Visible = options.Visible ?? CurrentState.Visible,
            FastCombine = options.FastCombine ?? CurrentState.FastCombine
        };
    }

    public void RestoreState(SessionState state)
    {
        _restoreHistory.Push(state);
        CurrentState = state;
    }

    public Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<IWorkbookHandle>(new FakeWorkbookHandle());

    public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkbookSummary>>(Array.Empty<WorkbookSummary>());

    public Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
