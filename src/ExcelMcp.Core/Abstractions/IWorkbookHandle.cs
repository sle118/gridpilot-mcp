namespace ExcelMcp.Core.Abstractions;

public interface IWorkbookHandle : IAsyncDisposable
{
    string Name { get; }
    string FullPath { get; }

    Task SaveAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default);

    Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default);
    Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default);
    Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default);
    Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default);
    Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default);

    Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default);
    Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default);
}
