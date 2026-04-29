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
}
