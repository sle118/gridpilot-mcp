using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using System.Runtime.Versioning;

namespace ExcelMcp.ComAdapter.Interop;

[SupportedOSPlatform("windows")]
internal sealed class ComExcelApplicationHandle : IExcelApplicationHandle
{
    private readonly object _application;
    private bool _disposed;

    private ComExcelApplicationHandle(object application)
    {
        _application = application;
    }

    public static ComExcelApplicationHandle AttachToRunningInstance()
    {
        var applicationType = Type.GetTypeFromProgID("Excel.Application")
            ?? throw new InvalidOperationException("Excel.Application COM progid is not available.");

        try
        {
            var application = Microsoft.VisualBasic.Interaction.GetObject(null, "Excel.Application");
            return new ComExcelApplicationHandle(application ?? throw new InvalidOperationException("Excel returned a null application handle."));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to attach to a running Excel instance.", ex);
        }
    }

    public static ComExcelApplicationHandle CreateNew(bool visible = false)
    {
        var applicationType = Type.GetTypeFromProgID("Excel.Application")
            ?? throw new InvalidOperationException("Excel.Application COM progid is not available.");

        var application = Activator.CreateInstance(applicationType)
            ?? throw new InvalidOperationException("Unable to create a new Excel.Application COM instance.");

        var handle = new ComExcelApplicationHandle(application);
        ComDispatch.SetProperty(application, "Visible", visible);
        return handle;
    }

    public SessionState CaptureState()
    {
        ThrowIfDisposed();

        return new SessionState(
            DisplayAlerts: ComDispatch.GetProperty<bool>(_application, "DisplayAlerts"),
            ScreenUpdating: ComDispatch.GetProperty<bool>(_application, "ScreenUpdating"),
            EnableEvents: ComDispatch.GetProperty<bool>(_application, "EnableEvents"),
            Visible: ComDispatch.GetProperty<bool>(_application, "Visible"),
            FastCombine: null);
    }

    public void ApplyOptions(SessionOptions options)
    {
        ThrowIfDisposed();

        if (options.DisplayAlerts is bool displayAlerts)
        {
            ComDispatch.SetProperty(_application, "DisplayAlerts", displayAlerts);
        }

        if (options.EnableEvents is bool enableEvents)
        {
            ComDispatch.SetProperty(_application, "EnableEvents", enableEvents);
        }

        if (options.ScreenUpdating is bool screenUpdating)
        {
            ComDispatch.SetProperty(_application, "ScreenUpdating", screenUpdating);
        }

        if (options.Visible is bool visible)
        {
            ComDispatch.SetProperty(_application, "Visible", visible);
        }
    }

    public void RestoreState(SessionState state)
    {
        ThrowIfDisposed();

        ComDispatch.SetProperty(_application, "DisplayAlerts", state.DisplayAlerts);
        ComDispatch.SetProperty(_application, "EnableEvents", state.EnableEvents);
        ComDispatch.SetProperty(_application, "ScreenUpdating", state.ScreenUpdating);
        ComDispatch.SetProperty(_application, "Visible", state.Visible);
    }

    public Task<IWorkbookHandle> OpenWorkbookAsync(string path, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var workbooks = ComDispatch.GetProperty<object>(_application, "Workbooks");
        try
        {
            var workbook = ComDispatch.InvokeMethod(workbooks, "Open", path)
                ?? throw new InvalidOperationException($"Excel did not return a workbook for '{path}'.");

            return Task.FromResult<IWorkbookHandle>(new ComWorkbookHandle(workbook));
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(workbooks);
        }
    }

    public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var workbooks = ComDispatch.GetProperty<object>(_application, "Workbooks");
        try
        {
            var activeWorkbook = ComDispatch.GetProperty<object?>(_application, "ActiveWorkbook");
            try
            {
                var summaries = new List<WorkbookSummary>();
                foreach (var workbook in ComDispatch.Enumerate(workbooks))
                {
                    summaries.Add(new WorkbookSummary(
                        Name: ComDispatch.GetProperty<string>(workbook, "Name"),
                        FullPath: ComDispatch.GetProperty<string>(workbook, "FullName"),
                        IsActive: ReferenceEquals(workbook, activeWorkbook)));

                    ComDispatch.ReleaseIfComObject(workbook);
                }

                return Task.FromResult<IReadOnlyList<WorkbookSummary>>(summaries);
            }
            finally
            {
                ComDispatch.ReleaseIfComObject(activeWorkbook);
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(workbooks);
        }
    }

    public Task WaitForAsyncQueriesAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        ComDispatch.InvokeMethod(_application, "CalculateUntilAsyncQueriesDone");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        ComDispatch.ReleaseIfComObject(_application);
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
