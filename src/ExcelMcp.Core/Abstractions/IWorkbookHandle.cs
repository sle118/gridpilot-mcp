using ExcelMcp.Core.Results;

namespace ExcelMcp.Core.Abstractions;

public interface IWorkbookHandle : IAsyncDisposable
{
    string Name { get; }
    string FullPath { get; }

    Task SaveAsync(CancellationToken cancellationToken = default);
    Task SaveAsAsync(string path, CancellationToken cancellationToken = default);
    Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NameSummary>> ListNamesAsync(CancellationToken cancellationToken = default);

    Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default);
    Task<NameSummary> GetNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default);
    Task CreateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default);
    Task UpdateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default);
    Task DeleteNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default);
    Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default);
    Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default);
    Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default);
    Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default);
    Task CreateWorksheetAsync(string sheetName, CancellationToken cancellationToken = default);
    Task RenameWorksheetAsync(string sheetName, string newSheetName, CancellationToken cancellationToken = default);
    Task DeleteWorksheetAsync(string sheetName, CancellationToken cancellationToken = default);
    Task<TableDetailResult> GetTableAsync(string tableName, CancellationToken cancellationToken = default);
    Task<TableReadResult> ReadTableAsync(string tableName, CancellationToken cancellationToken = default);
    Task CreateTableAsync(TableCreateRequest request, CancellationToken cancellationToken = default);
    Task ResizeTableAsync(TableResizeRequest request, CancellationToken cancellationToken = default);
    Task AppendTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default);
    Task ReplaceTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default);
    Task DeleteTableAsync(string tableName, CancellationToken cancellationToken = default);
    Task SetTableOptionsAsync(TableOptionsUpdateRequest request, CancellationToken cancellationToken = default);

    Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default);
    Task<RangeData> ReadNamedRangeAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default);
    Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default);
}
