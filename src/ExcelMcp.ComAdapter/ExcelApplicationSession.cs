using ExcelMcp.Bridge.Services;
using ExcelMcp.ComAdapter.Interop;
using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;

namespace ExcelMcp.ComAdapter;

public sealed class ExcelApplicationSession : IExcelSession
{
    private readonly IExcelApplicationHandle _application;
    private readonly SessionScopeManager _scopeManager;

    public ExcelApplicationSession(IExcelApplicationHandle application)
    {
        _application = application;
        _scopeManager = new SessionScopeManager(application);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static ExcelApplicationSession AttachToRunning(SessionAttachTarget? target = null) =>
        new(ComExcelApplicationHandle.AttachToRunningInstance(target ?? SessionAttachTarget.AnyRunningInstance));

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static ExcelApplicationSession CreateNew(bool visible = false) =>
        new(ComExcelApplicationHandle.CreateNew(visible));

    public ValueTask DisposeAsync() => _application.DisposeAsync();

    public Task<SessionState> GetStateAsync(CancellationToken cancellationToken = default) =>
        _scopeManager.GetStateAsync(cancellationToken);

    public Task<SessionDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_application.CaptureDiagnostics());

    public async Task<SessionOptionsScope> BeginScopeAsync(SessionOptions options, CancellationToken cancellationToken = default)
    {
        var token = await PushOptionsAsync(options, cancellationToken).ConfigureAwait(false);
        return new SessionOptionsScope(this, token);
    }

    public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
        _application.ListOpenWorkbooksAsync(cancellationToken);

    public Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default) =>
        _application.OpenWorkbookAsync(path, cancellationToken);

    public Task<ScopedSessionToken> PushOptionsAsync(SessionOptions options, CancellationToken cancellationToken = default) =>
        _scopeManager.PushOptionsAsync(options, cancellationToken);

    public Task PopOptionsAsync(ScopedSessionToken token, CancellationToken cancellationToken = default) =>
        _scopeManager.PopOptionsAsync(token, cancellationToken);

    public Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        _application.WaitForAsyncQueriesAsync(timeout, cancellationToken);
}
