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
    public List<(string SheetName, string Address)> ReadRangeCalls { get; } = [];
    public List<(string SheetName, string Address, object?[,] Values)> WriteRangeCalls { get; } = [];

    public IReadOnlyList<SheetSummary> Sheets { get; set; } = Array.Empty<SheetSummary>();
    public IReadOnlyList<TableSummary> Tables { get; set; } = Array.Empty<TableSummary>();
    public IReadOnlyList<QuerySummary> Queries { get; set; } = Array.Empty<QuerySummary>();
    public IReadOnlyList<ConnectionSummary> Connections { get; set; } = Array.Empty<ConnectionSummary>();
    public IReadOnlyList<NameSummary> Names { get; set; } = Array.Empty<NameSummary>();

    public Func<string, Task<QueryDefinition>> OnGetQueryAsync { get; set; } =
        name => Task.FromResult(new QueryDefinition(name, "let Source = 1 in Source"));

    public Func<string, Task<NameSummary>> OnGetNameAsync { get; set; } =
        name => Task.FromResult(new NameSummary(name, "Workbook", null, "=Sheet1!$A$1", "$A$1"));

    public Func<QueryProbeRequest, Task<ProbeResult>> OnRunProbeAsync { get; set; } =
        request => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));

    public Func<string, Task<CleanupResult>> OnCleanupAsync { get; set; } =
        pattern => Task.FromResult(new CleanupResult(0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OperationError>()));

    public Func<string, RefreshOptions?, Task<RefreshResult>> OnRefreshAsync { get; set; } =
        (queryName, options) => Task.FromResult(new RefreshResult(true, queryName, "query", TimeSpan.Zero));

    public Func<string, string?, Task<RangeData>> OnReadRangeAsync { get; set; } =
        (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { "value" } }));

    public Func<string, Task<RangeData>> OnReadNamedRangeAsync { get; set; } =
        name => Task.FromResult(new RangeData("Sheet1", "$A$1", new object?[,] { { "value" } }));

    public Func<string, Task<TableReadResult>> OnReadTableAsync { get; set; } =
        tableName => Task.FromResult(new TableReadResult(tableName, "Sheet1", "$A$1:$B$2", ["Column1", "Column2"], [[1d, 2d]], false));

    public Func<string, object?[,], string?, Task> OnWriteRangeAsync { get; set; } =
        (address, values, sheetName) => Task.CompletedTask;

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
    public Task<IReadOnlyList<NameSummary>> ListNamesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Names);
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => OnGetQueryAsync(queryName);
    public Task<NameSummary> GetNameAsync(string name, CancellationToken cancellationToken = default) => OnGetNameAsync(name);
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
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ReadRangeCalls.Add((sheetName ?? "Sheet1", address));
        return OnReadRangeAsync(address, sheetName);
    }

    public Task<RangeData> ReadNamedRangeAsync(string name, CancellationToken cancellationToken = default) =>
        OnReadNamedRangeAsync(name);

    public Task<TableReadResult> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        OnReadTableAsync(tableName);

    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeCalls.Add((sheetName ?? "Sheet1", address, values));
        return OnWriteRangeAsync(address, values, sheetName);
    }
}
