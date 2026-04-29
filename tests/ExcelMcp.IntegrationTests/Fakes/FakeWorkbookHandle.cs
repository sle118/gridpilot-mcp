using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;

namespace ExcelMcp.IntegrationTests.Fakes;

internal sealed class FakeWorkbookHandle : IWorkbookHandle
{
    public string Name => "fake.xlsx";
    public string FullPath => @"C:\temp\fake.xlsx";
    public IReadOnlyList<SheetSummary> Sheets { get; set; } = Array.Empty<SheetSummary>();
    public IReadOnlyList<TableSummary> Tables { get; set; } = Array.Empty<TableSummary>();
    public IReadOnlyList<QuerySummary> Queries { get; set; } = Array.Empty<QuerySummary>();
    public IReadOnlyList<ConnectionSummary> Connections { get; set; } = Array.Empty<ConnectionSummary>();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sheets);
    public Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tables);
    public Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Queries);
    public Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Connections);
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => Task.FromResult(new QueryDefinition(queryName, "let Source = 1 in Source"));
    public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new RefreshResult(true, queryName, "query", TimeSpan.Zero));
    public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));
    public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default) => Task.FromResult(new CleanupResult(0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OperationError>()));
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { "value" } }));
    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
