using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;

namespace ExcelMcp.UnitTests.Fakes;

internal sealed class FakeWorkbookHandle : IWorkbookHandle
{
    public string Name => "fake.xlsx";
    public string FullPath => @"C:\temp\fake.xlsx";
    public int SaveCallCount { get; private set; }
    public List<(string QueryName, RefreshOptions? Options)> RefreshCalls { get; } = [];
    public List<(string QueryName, string Formula)> SetQueryFormulaCalls { get; } = [];

    public IReadOnlyList<SheetSummary> Sheets { get; set; } = Array.Empty<SheetSummary>();
    public IReadOnlyList<TableSummary> Tables { get; set; } = Array.Empty<TableSummary>();
    public IReadOnlyList<QuerySummary> Queries { get; set; } = Array.Empty<QuerySummary>();
    public IReadOnlyList<ConnectionSummary> Connections { get; set; } = Array.Empty<ConnectionSummary>();

    public Func<string, Task<QueryDefinition>> OnGetQueryAsync { get; set; } =
        name => Task.FromResult(new QueryDefinition(name, "let Source = 1 in Source"));

    public Func<QueryProbeRequest, Task<ProbeResult>> OnRunProbeAsync { get; set; } =
        request => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));

    public Func<string, Task<CleanupResult>> OnCleanupAsync { get; set; } =
        pattern => Task.FromResult(new CleanupResult(0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OperationError>()));

    public Func<string, RefreshOptions?, Task<RefreshResult>> OnRefreshAsync { get; set; } =
        (queryName, options) => Task.FromResult(new RefreshResult(true, queryName, "query", TimeSpan.Zero));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        return Task.CompletedTask;
    }
    public Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sheets);
    public Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tables);
    public Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Queries);
    public Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Connections);
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => OnGetQueryAsync(queryName);
    public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default)
    {
        SetQueryFormulaCalls.Add((queryName, formula));
        return Task.CompletedTask;
    }
    public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default)
    {
        RefreshCalls.Add((queryName, options));
        return OnRefreshAsync(queryName, options);
    }
    public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => OnRunProbeAsync(request);
    public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default) => OnCleanupAsync(prefixOrPattern);
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { "value" } }));
    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
