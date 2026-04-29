using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class WorkbookOperationSafety
{
    private readonly IExcelSession _session;

    public WorkbookOperationSafety(IExcelSession session)
    {
        _session = session;
    }

    public async Task<OperationError?> CheckAsync(
        string workbookPath,
        WorkbookOperationIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (intent == WorkbookOperationIntent.ReadOnly)
        {
            return null;
        }

        var diagnostics = await _session.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        var normalizedTarget = NormalizePath(workbookPath);
        if (diagnostics.SessionMode == ExcelSessionMode.AttachToRunning)
        {
            var openWorkbooks = await _session.ListOpenWorkbooksAsync(cancellationToken).ConfigureAwait(false);
            var openWorkbook = openWorkbooks.FirstOrDefault(workbook =>
                string.Equals(NormalizePath(workbook.FullPath), normalizedTarget, StringComparison.OrdinalIgnoreCase));

            if (openWorkbook is not null)
            {
                return new OperationError(
                    Code: "shared_session_workbook_open",
                    Message: $"Operation '{GetOperationLabel(intent)}' is blocked because workbook '{openWorkbook.Name}' is already open in the attached Excel session.",
                    Detail: "Read-only inventory and query-definition operations are allowed, but mutating actions require an exclusive-safe workbook state.",
                    Source: nameof(WorkbookOperationSafety));
            }
        }

        if (!diagnostics.IsReady)
        {
            return new OperationError(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel is not ready for safe mutation.",
                Detail: "Excel reported a non-ready UI state, which can indicate an in-progress edit, modal dialog, or another transient application interaction.",
                Source: nameof(WorkbookOperationSafety));
        }

        if (!diagnostics.IsInteractive)
        {
            return new OperationError(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel is not interactive.",
                Detail: "Excel reported a non-interactive state, which can indicate modal UI or another automation-blocking condition.",
                Source: nameof(WorkbookOperationSafety));
        }

        if (diagnostics.CalculationState is ExcelCalculationState.Calculating or ExcelCalculationState.Pending)
        {
            return new OperationError(
                Code: "shared_session_busy",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel is in a transient calculation or refresh state.",
                Detail: $"Excel calculation state is '{diagnostics.CalculationState}'.",
                Source: nameof(WorkbookOperationSafety));
        }

        if (diagnostics.SessionMode == ExcelSessionMode.AttachToRunning)
        {
            return new OperationError(
                Code: "shared_session_attach_mutation_unsupported",
                Message: $"Operation '{GetOperationLabel(intent)}' is not yet supported in attached-session mutation mode.",
                Detail: "Attached live Excel sessions currently allow read-only inspection more broadly, but mutating operations remain blocked until stricter shared-session safeguards are implemented.",
                Source: nameof(WorkbookOperationSafety));
        }

        return null;
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

    private static string GetOperationLabel(WorkbookOperationIntent intent) =>
        intent switch
        {
            WorkbookOperationIntent.Mutating => "mutating",
            WorkbookOperationIntent.DiagnosticTempWrite => "diagnostic_temp_write",
            _ => "read_only"
        };
}
