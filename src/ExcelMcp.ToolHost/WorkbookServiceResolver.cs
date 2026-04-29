using ExcelMcp.Bridge.Services;
using ExcelMcp.ComAdapter;
using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using System.Runtime.Versioning;

namespace ExcelMcp.ToolHost;

[SupportedOSPlatform("windows")]
internal sealed class WorkbookServiceResolver : IWorkbookServiceResolver, IAsyncDisposable
{
    private readonly HostOptions _options;
    private readonly IExcelSession? _sharedSession;
    private readonly WorkbookService? _sharedService;

    private WorkbookServiceResolver(HostOptions options, IExcelSession? sharedSession)
    {
        _options = options;
        _sharedSession = sharedSession;
        _sharedService = sharedSession is null ? null : new WorkbookService(sharedSession);
    }

    public static Task<WorkbookServiceResolver> CreateAsync(HostOptions options)
    {
        IExcelSession? session = null;
        if (options.SessionMode == SessionMode.CreateNew)
        {
            session = ExcelApplicationSession.CreateNew(options.Visible);
        }
        else if (options.AttachTarget == SessionAttachTargetMode.AnyRunningInstance)
        {
            session = ExcelApplicationSession.AttachToRunning(SessionAttachTarget.AnyRunningInstance);
        }

        return Task.FromResult(new WorkbookServiceResolver(options, session));
    }

    public async Task<T> ExecuteAsync<T>(
        string workbookPath,
        Func<WorkbookService, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_sharedService is not null)
        {
            return await action(_sharedService).ConfigureAwait(false);
        }

        if (_options.SessionMode != SessionMode.Attach ||
            _options.AttachTarget != SessionAttachTargetMode.WorkbookOwner)
        {
            throw new InvalidOperationException("The workbook service resolver was not configured for on-demand workbook-targeted attachment.");
        }

        await using var session = ExcelApplicationSession.AttachToRunning(SessionAttachTarget.ForWorkbook(workbookPath));
        var service = new WorkbookService(session);
        return await action(service).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sharedSession is not null)
        {
            await _sharedSession.DisposeAsync().ConfigureAwait(false);
        }
    }
}
