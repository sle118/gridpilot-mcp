using ExcelMcp.Bridge.Services;
using ExcelMcp.ComAdapter.Interop;
using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Logging;

namespace ExcelMcp.ComAdapter;

public sealed class ExcelApplicationSession : IExcelSession
{
    private readonly IExcelApplicationHandle _application;
    private readonly SessionScopeManager _scopeManager;
    private readonly IGridPilotLogger _logger;

    public ExcelApplicationSession(IExcelApplicationHandle application, IGridPilotLogger? logger = null)
    {
        _application = application;
        _logger = logger ?? GridPilotNullLogger.Instance;
        _scopeManager = new SessionScopeManager(application, _logger);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static ExcelApplicationSession AttachToRunning(SessionAttachTarget? target = null, IGridPilotLogger? logger = null) =>
        new(ComExcelApplicationHandle.AttachToRunningInstance(target ?? SessionAttachTarget.AnyRunningInstance, logger), logger);

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static ExcelApplicationSession CreateNew(bool visible = false, IGridPilotLogger? logger = null) =>
        new(ComExcelApplicationHandle.CreateNew(visible, logger), logger);

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

    public Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(nameof(ExcelApplicationSession), "open_workbook_requested", new Dictionary<string, object?>
        {
            ["workbookPath"] = path
        });
        return _application.OpenWorkbookAsync(path, cancellationToken);
    }

    public Task<WorkbookSummary> EnsureWorkbookOpenAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(nameof(ExcelApplicationSession), "ensure_workbook_open_requested", new Dictionary<string, object?>
        {
            ["workbookPath"] = path
        });
        return _application.EnsureWorkbookOpenAsync(path, cancellationToken);
    }

    public Task<WorkbookSummary> CreateWorkbookAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(nameof(ExcelApplicationSession), "create_workbook_requested", new Dictionary<string, object?>
        {
            ["workbookPath"] = path
        });
        return _application.CreateWorkbookAsync(path, cancellationToken);
    }

    public Task<ScopedSessionToken> PushOptionsAsync(SessionOptions options, CancellationToken cancellationToken = default) =>
        _scopeManager.PushOptionsAsync(options, cancellationToken);

    public Task PopOptionsAsync(ScopedSessionToken token, CancellationToken cancellationToken = default) =>
        _scopeManager.PopOptionsAsync(token, cancellationToken);

    public Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        _application.WaitForAsyncQueriesAsync(timeout, cancellationToken);
}
