using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using System.Runtime.Versioning;

namespace ExcelMcp.ComAdapter.Interop;

[SupportedOSPlatform("windows")]
internal sealed class ComExcelApplicationHandle : IExcelApplicationHandle
{
    private readonly object _application;
    private readonly bool _ownsApplication;
    private readonly SessionAttachTargetMode? _attachTargetMode;
    private bool _disposed;

    private ComExcelApplicationHandle(object application, bool ownsApplication, SessionAttachTargetMode? attachTargetMode = null)
    {
        _application = application;
        _ownsApplication = ownsApplication;
        _attachTargetMode = attachTargetMode;
    }

    public static ComExcelApplicationHandle AttachToRunningInstance(SessionAttachTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.Mode switch
        {
            SessionAttachTargetMode.WorkbookOwner => AttachToWorkbookOwnerInstance(target.WorkbookPath!),
            _ => AttachToAnyRunningInstance()
        };
    }

    public static ComExcelApplicationHandle CreateNew(bool visible = false)
    {
        var applicationType = Type.GetTypeFromProgID("Excel.Application")
            ?? throw new InvalidOperationException("Excel.Application COM progid is not available.");

        var application = Activator.CreateInstance(applicationType)
            ?? throw new InvalidOperationException("Unable to create a new Excel.Application COM instance.");

        var handle = new ComExcelApplicationHandle(application, ownsApplication: true);
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

    public SessionDiagnostics CaptureDiagnostics()
    {
        ThrowIfDisposed();

        var isReady = ReadBooleanProperty("Ready", defaultValue: true);
        var isInteractive = ReadBooleanProperty("Interactive", defaultValue: true);
        var calculationState = ReadCalculationState();
        var isEditingCell = !isReady && isInteractive;
        var hasModalUi = !isInteractive;
        var isBusy = calculationState is ExcelCalculationState.Calculating or ExcelCalculationState.Pending;

        return new SessionDiagnostics(
            SessionMode: _ownsApplication ? ExcelSessionMode.CreateNew : ExcelSessionMode.AttachToRunning,
            IsReady: isReady,
            IsInteractive: isInteractive,
            CalculationState: calculationState,
            AttachTargetMode: _attachTargetMode,
            IsEditingCell: isEditingCell,
            HasModalUi: hasModalUi,
            IsBusy: isBusy);
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
            var normalizedPath = NormalizePath(path);
            foreach (var workbookObject in ComDispatch.Enumerate(workbooks))
            {
                var workbook = workbookObject;
                var keepWorkbook = false;
                try
                {
                    var fullName = ReadWorkbookFullName(workbook);
                    if (string.Equals(fullName, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        keepWorkbook = true;
                        return Task.FromResult<IWorkbookHandle>(new ComWorkbookHandle(workbook, closeOnDispose: false));
                    }
                }
                finally
                {
                    if (!keepWorkbook)
                    {
                        ComDispatch.ReleaseIfComObject(workbook);
                    }
                }
            }

            var openedWorkbook = ComDispatch.InvokeMethod(workbooks, "Open", path)
                ?? throw new InvalidOperationException($"Excel did not return a workbook for '{path}'.");

            return Task.FromResult<IWorkbookHandle>(new ComWorkbookHandle(openedWorkbook));
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
        try
        {
            if (_ownsApplication)
            {
                ComDispatch.TryInvokeMethod(_application, "Quit", out _);
            }
        }
        finally
        {
            ComDispatch.ReleaseIfComObject(_application);
        }

        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private bool ReadBooleanProperty(string propertyName, bool defaultValue)
    {
        if (!ComDispatch.TryGetProperty(_application, propertyName, out var value) || value is null)
        {
            return defaultValue;
        }

        return value switch
        {
            bool flag => flag,
            _ => Convert.ToInt32(value) != 0
        };
    }

    private ExcelCalculationState ReadCalculationState()
    {
        if (!ComDispatch.TryGetProperty(_application, "CalculationState", out var value) || value is null)
        {
            return ExcelCalculationState.Unknown;
        }

        var numericValue = Convert.ToInt32(value);
        return numericValue switch
        {
            0 => ExcelCalculationState.Done,
            1 => ExcelCalculationState.Calculating,
            2 => ExcelCalculationState.Pending,
            _ => ExcelCalculationState.Unknown
        };
    }

    private static ComExcelApplicationHandle AttachToAnyRunningInstance()
    {
        var applicationType = Type.GetTypeFromProgID("Excel.Application")
            ?? throw new InvalidOperationException("Excel.Application COM progid is not available.");

        try
        {
            var application = Microsoft.VisualBasic.Interaction.GetObject(null, "Excel.Application");
            if (application is null)
            {
                throw new ExcelSessionTargetException(
                    "attach_target_no_running_instance",
                    "No running Excel instance was available for generic attachment.");
            }

            return new ComExcelApplicationHandle(application, ownsApplication: false, attachTargetMode: SessionAttachTargetMode.AnyRunningInstance);
        }
        catch (ExcelSessionTargetException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExcelSessionTargetException(
                "attach_target_no_running_instance",
                "Unable to attach to a running Excel instance.",
                "Start Excel first or switch the host to create-new session mode.",
                ex);
        }
    }

    private static ComExcelApplicationHandle AttachToWorkbookOwnerInstance(string workbookPath)
    {
        var candidates = RunningWorkbookObjectTable.FindWorkbookOwnerApplications(workbookPath);
        if (candidates.Count == 0)
        {
            throw new ExcelSessionTargetException(
                "attach_target_no_matching_instance",
                $"No running Excel instance currently has workbook '{NormalizePath(workbookPath)}' open.",
                "Open the workbook in Excel first, or switch the host to create-new or any-running attach mode.");
        }

        if (candidates.Count > 1)
        {
            foreach (var candidate in candidates)
            {
                ComDispatch.ReleaseIfComObject(candidate);
            }

            throw new ExcelSessionTargetException(
                "attach_target_multiple_matching_instances",
                $"Multiple running Excel instances appear to have workbook '{NormalizePath(workbookPath)}' open.",
                "Close duplicate workbook instances or choose a less ambiguous attachment mode.");
        }

        return new ComExcelApplicationHandle(candidates[0], ownsApplication: false, attachTargetMode: SessionAttachTargetMode.WorkbookOwner);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static string ReadWorkbookFullName(object workbook)
    {
        var fullName = ComDispatch.GetProperty<string>(workbook, "FullName");
        return NormalizePath(fullName);
    }
}
