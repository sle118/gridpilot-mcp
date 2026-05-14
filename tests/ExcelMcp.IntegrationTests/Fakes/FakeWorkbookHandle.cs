using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;
using System.IO;

namespace ExcelMcp.IntegrationTests.Fakes;

internal sealed class FakeWorkbookHandle : IWorkbookHandle
{
    public string Name => Path.GetFileName(FullPath);
    public string FullPath { get; private set; } = @"C:\temp\fake.xlsx";
    public IReadOnlyList<SheetSummary> Sheets { get; set; } = Array.Empty<SheetSummary>();
    public IReadOnlyList<TableSummary> Tables { get; set; } = Array.Empty<TableSummary>();
    public IReadOnlyList<QuerySummary> Queries { get; set; } = Array.Empty<QuerySummary>();
    public IReadOnlyList<ConnectionSummary> Connections { get; set; } = Array.Empty<ConnectionSummary>();
    public IReadOnlyList<NameSummary> Names { get; set; } = Array.Empty<NameSummary>();
    public QueryDetail QueryDetail { get; set; } = new("SalesQuery", "let Source = 1 in Source", null, QueryLoadModes.None, null, null, null, "query:SalesQuery");
    public ConnectionDetail ConnectionDetail { get; set; } = new("Query - SalesQuery", "2", true, null, null, null, null, "SalesQuery", Array.Empty<string>(), "connection:Query - SalesQuery");
    public WorkbookDependencyGraph DependencyGraph { get; set; } = new(Array.Empty<WorkbookDependencyNode>(), Array.Empty<WorkbookDependencyEdge>());
    public WorkbookStructureState WorkbookStructureState { get; set; } = new(WorkbookVisibilityModes.Visible, new WorkbookProtectionState(false, false, false));
    public WorkbookProtectionState WorkbookProtectionState { get; set; } = new(false, false, false);
    public List<QueryCreateRequest> CreatedQueries { get; } = [];
    public List<QueryRenameRequest> RenamedQueries { get; } = [];
    public List<string> DeletedQueries { get; } = [];
    public List<ConnectionRenameRequest> RenamedConnections { get; } = [];
    public List<ConnectionUpdateRequest> UpdatedConnections { get; } = [];
    public List<string> DeletedConnections { get; } = [];
    public List<WorkbookVisibilityRequest> WorkbookVisibilityChanges { get; } = [];
    public List<WorkbookProtectionUpdateRequest> WorkbookProtectionChanges { get; } = [];
    public List<(string QueryName, string Formula)> SetQueryFormulaCalls { get; } = [];
    public List<(string Name, string RefersTo, string? SheetName)> CreatedNames { get; } = [];
    public List<(string Name, string RefersTo, string? SheetName)> UpdatedNames { get; } = [];
    public List<(string Name, string? SheetName)> DeletedNames { get; } = [];
    public List<TableCreateRequest> CreatedTables { get; } = [];
    public List<TableResizeRequest> ResizedTables { get; } = [];
    public List<TableRowsWriteRequest> AppendedTableRows { get; } = [];
    public List<TableRowsWriteRequest> ReplacedTableRows { get; } = [];
    public List<string> DeletedTables { get; } = [];
    public List<TableOptionsUpdateRequest> UpdatedTableOptions { get; } = [];
    public List<(string SheetName, string Address, object?[,] Values)> WriteRangeCalls { get; } = [];
    public List<(string SheetName, string Address)> ReadRangeCalls { get; } = [];
    public List<(string SheetName, string Address)> ReadRangeFormulaCalls { get; } = [];
    public List<(string SheetName, string Address)> ReadRangeFormatCalls { get; } = [];
    public List<(string SheetName, string Address, string?[,] Formulas)> WriteRangeFormulaCalls { get; } = [];
    public List<(string SheetName, string Address, RangeFormatPatch Format)> WriteRangeFormatCalls { get; } = [];
    public List<(string SheetName, string Address, string Dimension)> AutofitRangeCalls { get; } = [];
    public List<(string SheetName, string Address)> ClearRangeCalls { get; } = [];
    public List<CalculationRequest> RecalculationCalls { get; } = [];
    public List<ErrorInspectionRequest> ErrorInspectionCalls { get; } = [];
    public List<string> CreatedWorksheets { get; } = [];
    public List<(string SheetName, string NewSheetName)> RenamedWorksheets { get; } = [];
    public List<string> DeletedWorksheets { get; } = [];
    public List<WorksheetMoveRequest> MovedWorksheets { get; } = [];
    public List<WorksheetCopyRequest> CopiedWorksheets { get; } = [];
    public List<WorksheetVisibilityRequest> WorksheetVisibilityChanges { get; } = [];
    public Func<QueryProbeRequest, Task<ProbeResult>> OnRunProbeAsync { get; set; } =
        request => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));
    public int SaveCallCount { get; private set; }
    public int SaveAsCallCount { get; private set; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        return Task.CompletedTask;
    }
    public Task SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        SaveAsCallCount++;
        FullPath = path;
        return Task.CompletedTask;
    }
    public Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Sheets);
    public Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Tables);
    public Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Queries);
    public Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Connections);
    public Task<IReadOnlyList<NameSummary>> ListNamesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Names);
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => Task.FromResult(new QueryDefinition(queryName, "let Source = 1 in Source"));
    public Task<QueryDetail> GetQueryDetailAsync(string queryName, CancellationToken cancellationToken = default) => Task.FromResult(QueryDetail with { Name = queryName, DependencyNodeId = $"query:{queryName}" });
    public Task CreateQueryAsync(QueryCreateRequest request, CancellationToken cancellationToken = default)
    {
        CreatedQueries.Add(request);
        return Task.CompletedTask;
    }

    public Task RenameQueryAsync(QueryRenameRequest request, CancellationToken cancellationToken = default)
    {
        RenamedQueries.Add(request);
        return Task.CompletedTask;
    }

    public Task DeleteQueryAsync(string queryName, CancellationToken cancellationToken = default)
    {
        DeletedQueries.Add(queryName);
        return Task.CompletedTask;
    }

    public Task<ConnectionDetail> GetConnectionAsync(string connectionName, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConnectionDetail with { Name = connectionName, DependencyNodeId = $"connection:{connectionName}" });

    public Task RenameConnectionAsync(ConnectionRenameRequest request, CancellationToken cancellationToken = default)
    {
        RenamedConnections.Add(request);
        return Task.CompletedTask;
    }

    public Task UpdateConnectionAsync(ConnectionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        UpdatedConnections.Add(request);
        return Task.CompletedTask;
    }

    public Task DeleteConnectionAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        DeletedConnections.Add(connectionName);
        return Task.CompletedTask;
    }

    public Task<WorkbookDependencyGraph> GetDependencyGraphAsync(CancellationToken cancellationToken = default) => Task.FromResult(DependencyGraph);

    public Task<WorkbookStructureState> GetWorkbookStructureStateAsync(CancellationToken cancellationToken = default) => Task.FromResult(WorkbookStructureState);

    public Task<WorkbookProtectionState> GetWorkbookProtectionStateAsync(CancellationToken cancellationToken = default) => Task.FromResult(WorkbookProtectionState);

    public Task SetWorkbookVisibilityAsync(WorkbookVisibilityRequest request, CancellationToken cancellationToken = default)
    {
        WorkbookVisibilityChanges.Add(request);
        return Task.CompletedTask;
    }

    public Task SetWorkbookProtectionAsync(WorkbookProtectionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        WorkbookProtectionChanges.Add(request);
        return Task.CompletedTask;
    }

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
    public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => OnRunProbeAsync(request);
    public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default) => Task.FromResult(new CleanupResult(0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OperationError>()));
    public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ReadRangeCalls.Add((sheetName ?? "Sheet1", address));
        return Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, CreateMatrixForAddress(address)));
    }

    public Task<RangeData> ReadRangeFormulasAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ReadRangeFormulaCalls.Add((sheetName ?? "Sheet1", address));
        return Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, CreateFormulaMatrixForAddress(address)));
    }

    public Task<RangeFormatData> ReadRangeFormatAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ReadRangeFormatCalls.Add((sheetName ?? "Sheet1", address));
        return Task.FromResult(new RangeFormatData(
            sheetName ?? "Sheet1",
            address,
            new RangeFormatSnapshot(Bold: true, HasFill: true, FillColor: "#FFFFFF", HorizontalAlignment: "center"),
            Array.Empty<string>()));
    }

    public Task<RangeData> ReadNamedRangeAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RangeData(sheetName ?? "Sheet1", "$A$1", new object?[,] { { "value" } }));

    public Task<TableReadResult> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TableReadResult(tableName, "Sheet1", "$A$1:$B$2", ["Column1", "Column2"], [[1d, 2d]], false));

    public Task<TableDetailResult> GetTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TableDetailResult(tableName, "Sheet1", "$A$1:$B$2", ["Column1", "Column2"], 1, 2, true, false, false, null));

    public Task CreateTableAsync(TableCreateRequest request, CancellationToken cancellationToken = default)
    {
        CreatedTables.Add(request);
        return Task.CompletedTask;
    }

    public Task ResizeTableAsync(TableResizeRequest request, CancellationToken cancellationToken = default)
    {
        ResizedTables.Add(request);
        return Task.CompletedTask;
    }

    public Task AppendTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default)
    {
        AppendedTableRows.Add(request);
        return Task.CompletedTask;
    }

    public Task ReplaceTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default)
    {
        ReplacedTableRows.Add(request);
        return Task.CompletedTask;
    }

    public Task DeleteTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        DeletedTables.Add(tableName);
        return Task.CompletedTask;
    }

    public Task SetTableOptionsAsync(TableOptionsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        UpdatedTableOptions.Add(request);
        return Task.CompletedTask;
    }

    public Task RecalculateAsync(CalculationRequest request, CancellationToken cancellationToken = default)
    {
        RecalculationCalls.Add(request);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ErrorInspectionHit>> InspectErrorsAsync(ErrorInspectionRequest request, CancellationToken cancellationToken = default)
    {
        ErrorInspectionCalls.Add(request);
        return Task.FromResult<IReadOnlyList<ErrorInspectionHit>>(
        [
            new ErrorInspectionHit(
                request.SheetName ?? "Sheet1",
                request.Address ?? "$A$1",
                HasFormula: true,
                Formula: "=1/0",
                ErrorCode: "#DIV/0!",
                ValueKind: "formula_error")
        ]);
    }

    public Task CreateWorksheetAsync(string sheetName, CancellationToken cancellationToken = default)
    {
        CreatedWorksheets.Add(sheetName);
        return Task.CompletedTask;
    }

    public Task RenameWorksheetAsync(string sheetName, string newSheetName, CancellationToken cancellationToken = default)
    {
        RenamedWorksheets.Add((sheetName, newSheetName));
        return Task.CompletedTask;
    }

    public Task DeleteWorksheetAsync(string sheetName, CancellationToken cancellationToken = default)
    {
        DeletedWorksheets.Add(sheetName);
        return Task.CompletedTask;
    }

    public Task MoveWorksheetAsync(WorksheetMoveRequest request, CancellationToken cancellationToken = default)
    {
        MovedWorksheets.Add(request);
        return Task.CompletedTask;
    }

    public Task CopyWorksheetAsync(WorksheetCopyRequest request, CancellationToken cancellationToken = default)
    {
        CopiedWorksheets.Add(request);
        return Task.CompletedTask;
    }

    public Task SetWorksheetVisibilityAsync(WorksheetVisibilityRequest request, CancellationToken cancellationToken = default)
    {
        WorksheetVisibilityChanges.Add(request);
        return Task.CompletedTask;
    }

    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeCalls.Add((sheetName ?? "Sheet1", address, values));
        return Task.CompletedTask;
    }

    public Task WriteRangeFormulasAsync(string address, string?[,] formulas, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeFormulaCalls.Add((sheetName ?? "Sheet1", address, formulas));
        return Task.CompletedTask;
    }

    public Task WriteRangeFormatAsync(string address, RangeFormatPatch format, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeFormatCalls.Add((sheetName ?? "Sheet1", address, format));
        return Task.CompletedTask;
    }

    public Task AutofitRangeAsync(string address, string dimension, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        AutofitRangeCalls.Add((sheetName ?? "Sheet1", address, dimension));
        return Task.CompletedTask;
    }

    public Task ClearRangeContentsAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ClearRangeCalls.Add((sheetName ?? "Sheet1", address));
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

    private static object?[,] CreateFormulaMatrixForAddress(string address)
    {
        var (rows, columns) = GetRangeSize(address);
        var matrix = new object?[rows, columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                matrix[row, column] = row == 0 && column == 0 ? "=1+1" : null;
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
