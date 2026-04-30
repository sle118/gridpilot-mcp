using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class WorkbookService
{
    private readonly IExcelSession _session;
    private readonly WorkbookOperationSafety _operationSafety;
    private static readonly SessionOptions QuietSessionOptions = new(
        DisplayAlerts: false,
        ScreenUpdating: false,
        EnableEvents: false);

    public WorkbookService(IExcelSession session, WorkbookOperationSafety? operationSafety = null)
    {
        _session = session;
        _operationSafety = operationSafety ?? new WorkbookOperationSafety(session);
    }

    public async Task<QueryDefinition> GetQueryAsync(string workbookPath, string queryName, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetQueryAsync(queryName, cancellationToken);
    }

    public async Task<NameSummary> GetNameAsync(string workbookPath, string name, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.GetNameAsync(name, sheetName, cancellationToken);
    }

    public async Task<NameMutationResult> CreateNameAsync(
        string workbookPath,
        string name,
        string refersTo,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new NameMutationResult(false, workbookPath, name, "create", GetScope(sheetName), sheetName, refersTo, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.CreateNameAsync(name, refersTo, sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            return new NameMutationResult(true, workbookPath, name, "create", GetScope(sheetName), sheetName, refersTo);
        }
        catch (Exception ex)
        {
            return BuildNameMutationError(workbookPath, name, "create", sheetName, refersTo, ex);
        }
    }

    public async Task<NameMutationResult> UpdateNameAsync(
        string workbookPath,
        string name,
        string refersTo,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new NameMutationResult(false, workbookPath, name, "update", GetScope(sheetName), sheetName, refersTo, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.UpdateNameAsync(name, refersTo, sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            return new NameMutationResult(true, workbookPath, name, "update", GetScope(sheetName), sheetName, refersTo);
        }
        catch (Exception ex)
        {
            return BuildNameMutationError(workbookPath, name, "update", sheetName, refersTo, ex);
        }
    }

    public async Task<NameMutationResult> DeleteNameAsync(
        string workbookPath,
        string name,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new NameMutationResult(false, workbookPath, name, "delete", GetScope(sheetName), sheetName, null, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.DeleteNameAsync(name, sheetName, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            return new NameMutationResult(true, workbookPath, name, "delete", GetScope(sheetName), sheetName);
        }
        catch (Exception ex)
        {
            return BuildNameMutationError(workbookPath, name, "delete", sheetName, null, ex);
        }
    }

    public async Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListSheetsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TableSummary>> ListTablesAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListTablesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListQueriesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListConnectionsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NameSummary>> ListNamesAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ListNamesAsync(cancellationToken);
    }

    public async Task<WorkbookInventory> ListInventoryAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var sheets = await workbook.ListSheetsAsync(cancellationToken);
        var tables = await workbook.ListTablesAsync(cancellationToken);
        var queries = await workbook.ListQueriesAsync(cancellationToken);
        var connections = await workbook.ListConnectionsAsync(cancellationToken);
        return new WorkbookInventory(sheets, tables, queries, connections);
    }

    public async Task<RefreshResult> RefreshQueryAsync(string workbookPath, string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new RefreshOptions();
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RefreshResult(false, queryName, "blocked", TimeSpan.Zero, safetyError);
        }

        if (options.Silent)
        {
            await using var _ = await _session.BeginScopeAsync(QuietSessionOptions, cancellationToken);
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            var result = await workbook.RefreshQueryAsync(queryName, options, cancellationToken);
            if (result.Succeeded)
            {
                await workbook.SaveAsync(cancellationToken);
            }

            return result;
        }

        await using (var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken))
        {
            var result = await workbook.RefreshQueryAsync(queryName, options, cancellationToken);
            if (result.Succeeded)
            {
                await workbook.SaveAsync(cancellationToken);
            }

            return result;
        }
    }

    public async Task<ProbeResult> TryRunQueryAsync(string workbookPath, string queryName, string tempPrefix, CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.DiagnosticTempWrite, cancellationToken);
        if (safetyError is not null)
        {
            return new ProbeResult(false, queryName, tempPrefix, null, safetyError);
        }

        await using var _ = await _session.BeginScopeAsync(QuietSessionOptions, cancellationToken);
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var tempName = $"{tempPrefix}_{queryName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        return await workbook.RunQueryProbeAsync(new QueryProbeRequest(queryName, tempName), cancellationToken);
    }

    public async Task<CleanupResult> CleanupTempQueriesAsync(string workbookPath, string pattern, CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.DiagnosticTempWrite, cancellationToken);
        if (safetyError is not null)
        {
            return new CleanupResult(
                DeletedCount: 0,
                DeletedNames: Array.Empty<string>(),
                FailedNames: Array.Empty<string>(),
                Errors: new[] { safetyError });
        }

        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var result = await workbook.CleanupTempQueriesAsync(pattern, cancellationToken);
        if (result.DeletedCount > 0)
        {
            await workbook.SaveAsync(cancellationToken);
        }

        return result;
    }

    public async Task<QueryFormulaUpdateResult> SetQueryFormulaAsync(
        string workbookPath,
        string queryName,
        string formula,
        CancellationToken cancellationToken = default)
    {
        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new QueryFormulaUpdateResult(false, workbookPath, queryName, safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            await workbook.SetQueryFormulaAsync(queryName, formula, cancellationToken);
            await workbook.SaveAsync(cancellationToken);
            return new QueryFormulaUpdateResult(true, workbookPath, queryName);
        }
        catch (Exception ex)
        {
            return new QueryFormulaUpdateResult(
                false,
                workbookPath,
                queryName,
                new OperationError(
                    Code: "query_formula_update_failed",
                    Message: $"Failed to set formula for query '{queryName}'.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    public async Task<RangeReadResult> ReadRangeAsync(
        string workbookPath,
        string sheetName,
        string address,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var range = await workbook.ReadRangeAsync(address, sheetName, cancellationToken);
        return new RangeReadResult(
            range.SheetName,
            range.Address,
            ConvertValues(range.Values));
    }

    public async Task<RangeReadResult> ReadNamedRangeAsync(
        string workbookPath,
        string name,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var range = await workbook.ReadNamedRangeAsync(name, sheetName, cancellationToken);
        return new RangeReadResult(
            range.SheetName,
            range.Address,
            ConvertValues(range.Values));
    }

    public async Task<TableReadResult> ReadTableAsync(
        string workbookPath,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.ReadTableAsync(tableName, cancellationToken);
    }

    public async Task<RangeWriteResult> WriteRangesAsync(
        string workbookPath,
        RangeWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var safetyError = await _operationSafety.CheckAsync(workbookPath, WorkbookOperationIntent.Mutating, cancellationToken);
        if (safetyError is not null)
        {
            return new RangeWriteResult(false, workbookPath, 0, Array.Empty<string>(), safetyError);
        }

        try
        {
            await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
            foreach (var write in request.Writes)
            {
                await PreflightWriteAsync(workbook, write, cancellationToken);
            }

            var appliedWrites = new List<string>(request.Writes.Count);
            foreach (var write in request.Writes)
            {
                await workbook.WriteRangeAsync(write.Address, write.Values, write.SheetName, cancellationToken);
                appliedWrites.Add(write.Identifier);
            }

            await workbook.SaveAsync(cancellationToken);
            return new RangeWriteResult(true, workbookPath, appliedWrites.Count, appliedWrites);
        }
        catch (Exception ex)
        {
            return new RangeWriteResult(
                false,
                workbookPath,
                0,
                Array.Empty<string>(),
                new OperationError(
                    Code: "range_write_failed",
                    Message: "Failed to write one or more workbook ranges.",
                    Detail: ex.Message,
                    Source: nameof(WorkbookService)));
        }
    }

    private static async Task PreflightWriteAsync(
        IWorkbookHandle workbook,
        RangeWriteTarget write,
        CancellationToken cancellationToken)
    {
        ValidateValues(write.Values, write.Identifier);
        var existing = await workbook.ReadRangeAsync(write.Address, write.SheetName, cancellationToken);
        var expectedRows = GetRowCount(write.Values);
        var expectedColumns = GetColumnCount(write.Values);
        var actualRows = GetRowCount(existing.Values);
        var actualColumns = GetColumnCount(existing.Values);

        if (expectedRows != actualRows || expectedColumns != actualColumns)
        {
            throw new InvalidOperationException(
                $"Write target '{write.Identifier}' has shape {actualRows}x{actualColumns}, but provided values have shape {expectedRows}x{expectedColumns}.");
        }
    }

    private static void ValidateValues(object?[,] values, string identifier)
    {
        if (values.Length == 0)
        {
            throw new InvalidOperationException($"Write target '{identifier}' requires at least one value.");
        }
    }

    private static int GetRowCount(Array values) =>
        values.GetLength(0);

    private static int GetColumnCount(Array values) =>
        values.GetLength(1);

    private static IReadOnlyList<IReadOnlyList<object?>> ConvertValues(object?[,] values)
    {
        var rows = new List<IReadOnlyList<object?>>();
        for (var row = values.GetLowerBound(0); row <= values.GetUpperBound(0); row++)
        {
            var columns = new List<object?>();
            for (var column = values.GetLowerBound(1); column <= values.GetUpperBound(1); column++)
            {
                columns.Add(values[row, column]);
            }

            rows.Add(columns);
        }

        return rows;
    }

    private static string GetScope(string? sheetName) =>
        string.IsNullOrWhiteSpace(sheetName) ? "Workbook" : "Worksheet";

    private static NameMutationResult BuildNameMutationError(
        string workbookPath,
        string name,
        string action,
        string? sheetName,
        string? refersTo,
        Exception ex) =>
        new(
            false,
            workbookPath,
            name,
            action,
            GetScope(sheetName),
            sheetName,
            refersTo,
            new OperationError(
                Code: $"name_{action}_failed",
                Message: $"Failed to {action} name '{name}'.",
                Detail: ex.Message,
                Source: nameof(WorkbookService)));
}
