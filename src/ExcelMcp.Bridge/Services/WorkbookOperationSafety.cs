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

        var normalizedTarget = NormalizePath(workbookPath);
        var openWorkbooks = await _session.ListOpenWorkbooksAsync(cancellationToken).ConfigureAwait(false);
        var openWorkbook = openWorkbooks.FirstOrDefault(workbook =>
            string.Equals(NormalizePath(workbook.FullPath), normalizedTarget, StringComparison.OrdinalIgnoreCase));

        if (openWorkbook is null)
        {
            return null;
        }

        return new OperationError(
            Code: "shared_session_unsafe",
            Message: $"Operation '{GetOperationLabel(intent)}' is blocked because workbook '{openWorkbook.Name}' is already open in the attached Excel session.",
            Detail: "Read-only inventory and query-definition operations are allowed, but mutating actions require an exclusive-safe workbook state.",
            Source: nameof(WorkbookOperationSafety));
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
