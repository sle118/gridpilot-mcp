using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;

namespace ExcelMcp.UnitTests.Fakes;

internal sealed class FakeWorkbookHandle : IWorkbookHandle
{
    public string Name => "fake.xlsx";
    public string FullPath => @"C:\temp\fake.xlsx";

    public Func<string, Task<QueryDefinition>> OnGetQueryAsync { get; set; } =
        name => Task.FromResult(new QueryDefinition(name, "let Source = 1 in Source"));

    public Func<QueryProbeRequest, Task<ProbeResult>> OnRunProbeAsync { get; set; } =
        request => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));

    public Func<string, Task<CleanupResult>> OnCleanupAsync { get; set; } =
        pattern => Task.FromResult(new CleanupResult(0, Array.Empty<string>()));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SheetSummary>>(Array.Empty<SheetSummary>());
    public Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TableSummary>>(Array.Empty<TableSummary>());
    public Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QuerySummary>>(Array.Empty<QuerySummary>());
    public Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ConnectionSummary>>(Array.Empty<ConnectionSummary>());
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => OnGetQueryAsync(queryName);
    public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new RefreshResult(true, queryName, "query", TimeSpan.Zero));
    public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => OnRunProbeAsync(request);
    public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default) => OnCleanupAsync(prefixOrPattern);
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { "value" } }));
    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
