using ExcelMcp.Core;
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
    public IReadOnlyList<NameSummary> Names { get; set; } = Array.Empty<NameSummary>();
    public List<(string QueryName, string Formula)> SetQueryFormulaCalls { get; } = [];
    public List<(string Name, string RefersTo, string? SheetName)> CreatedNames { get; } = [];
    public List<(string Name, string RefersTo, string? SheetName)> UpdatedNames { get; } = [];
    public List<(string Name, string? SheetName)> DeletedNames { get; } = [];
    public List<(string SheetName, string Address, object?[,] Values)> WriteRangeCalls { get; } = [];
    public List<(string SheetName, string Address)> ReadRangeCalls { get; } = [];
    public int SaveCallCount { get; private set; }

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
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => Task.FromResult(new QueryDefinition(queryName, "let Source = 1 in Source"));
    public Task<NameSummary> GetNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) => Task.FromResult(new NameSummary(name, sheetName is null ? "Workbook" : "Worksheet", sheetName, "=Sheet1!$A$1", "$A$1"));
    public Task CreateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        CreatedNames.Add((name, refersTo, sheetName));
        return Task.CompletedTask;
    }
    public Task UpdateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        UpdatedNames.Add((name, refersTo, sheetName));
        return Task.CompletedTask;
    }
    public Task DeleteNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        DeletedNames.Add((name, sheetName));
        return Task.CompletedTask;
    }
    public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default)
    {
        SetQueryFormulaCalls.Add((queryName, formula));
        return Task.CompletedTask;
    }
    public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new RefreshResult(true, queryName, "query", TimeSpan.Zero));
    public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));
    public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default) => Task.FromResult(new CleanupResult(0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OperationError>()));
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ReadRangeCalls.Add((sheetName ?? "Sheet1", address));
        return Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, CreateMatrixForAddress(address)));
    }

    public Task<RangeData> ReadNamedRangeAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RangeData(sheetName ?? "Sheet1", "$A$1", new object?[,] { { "value" } }));

    public Task<TableReadResult> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TableReadResult(tableName, "Sheet1", "$A$1:$B$2", ["Column1", "Column2"], [[1d, 2d]], false));

    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeCalls.Add((sheetName ?? "Sheet1", address, values));
        return Task.CompletedTask;
    }

    private static object?[,] CreateMatrixForAddress(string address)
    {
        var (rows, columns) = GetRangeSize(address);
        var matrix = new object?[rows, columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                matrix[row, column] = row == 0 && column == 0 ? "value" : null;
            }
        }

        return matrix;
    }

    private static (int Rows, int Columns) GetRangeSize(string address)
    {
        var parts = address.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return (1, 1);
        }

        var (startColumn, startRow) = ParseCell(parts[0]);
        var (endColumn, endRow) = ParseCell(parts[1]);
        return (Math.Abs(endRow - startRow) + 1, Math.Abs(endColumn - startColumn) + 1);
    }

    private static (int Column, int Row) ParseCell(string cell)
    {
        var letters = new string(cell.TakeWhile(char.IsLetter).ToArray());
        var digits = new string(cell.SkipWhile(char.IsLetter).ToArray());

        var column = 0;
        foreach (var letter in letters.ToUpperInvariant())
        {
            column = (column * 26) + (letter - 'A' + 1);
        }

        return (column, int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture));
    }
}
