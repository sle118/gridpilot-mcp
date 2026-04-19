using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class WorkbookService
{
    private readonly IExcelSession _session;

    public WorkbookService(IExcelSession session)
    {
        _session = session;
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

    public async Task<ProbeResult> TryRunQueryAsync(string workbookPath, string queryName, string tempPrefix, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        var tempName = $"{tempPrefix}_{queryName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        return await workbook.RunQueryProbeAsync(new QueryProbeRequest(queryName, tempName), cancellationToken);
    }

    public async Task<CleanupResult> CleanupTempQueriesAsync(string workbookPath, string pattern, CancellationToken cancellationToken = default)
    {
        await using var workbook = await _session.OpenWorkbookAsync(workbookPath, cancellationToken);
        return await workbook.CleanupTempQueriesAsync(pattern, cancellationToken);
    }
}
