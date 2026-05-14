using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;
using System.IO;

namespace ExcelMcp.UnitTests.Fakes;

internal sealed class FakeWorkbookHandle : IWorkbookHandle
{
    public string Name => Path.GetFileName(FullPath);
    public string FullPath { get; private set; } = @"C:\temp\fake.xlsx";
    public int SaveCallCount { get; private set; }
    public int SaveAsCallCount { get; private set; }
    public List<(string QueryName, RefreshOptions? Options)> RefreshCalls { get; } = [];
    public List<(string QueryName, string Formula)> SetQueryFormulaCalls { get; } = [];
    public List<QueryCreateRequest> CreatedQueries { get; } = [];
    public List<QueryRenameRequest> RenamedQueries { get; } = [];
    public List<string> DeletedQueries { get; } = [];
    public List<ConnectionRenameRequest> RenamedConnections { get; } = [];
    public List<ConnectionUpdateRequest> UpdatedConnections { get; } = [];
    public List<string> DeletedConnections { get; } = [];
    public List<WorkbookVisibilityRequest> WorkbookVisibilityChanges { get; } = [];
    public List<WorkbookProtectionUpdateRequest> WorkbookProtectionChanges { get; } = [];
    public List<(string SheetName, string Address)> ReadRangeCalls { get; } = [];
    public List<(string SheetName, string Address)> ReadRangeFormulaCalls { get; } = [];
    public List<(string SheetName, string Address, object?[,] Values)> WriteRangeCalls { get; } = [];
    public List<(string SheetName, string Address, string?[,] Formulas)> WriteRangeFormulaCalls { get; } = [];
    public List<(string SheetName, string Address, RangeFormatPatch Format)> WriteRangeFormatCalls { get; } = [];
    public List<(string SheetName, string Address)> ReadRangeFormatCalls { get; } = [];
    public List<(string SheetName, string Address, string Dimension)> AutofitRangeCalls { get; } = [];
    public List<(string SheetName, string Address)> ClearRangeCalls { get; } = [];
    public List<CalculationRequest> RecalculationCalls { get; } = [];
    public List<ErrorInspectionRequest> ErrorInspectionCalls { get; } = [];

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
    public List<(string Name, string RefersTo, string? SheetName)> CreatedNames { get; } = [];
    public List<(string Name, string RefersTo, string? SheetName)> UpdatedNames { get; } = [];
    public List<(string Name, string? SheetName)> DeletedNames { get; } = [];
    public List<TableCreateRequest> CreatedTables { get; } = [];
    public List<TableResizeRequest> ResizedTables { get; } = [];
    public List<TableRowsWriteRequest> AppendedTableRows { get; } = [];
    public List<TableRowsWriteRequest> ReplacedTableRows { get; } = [];
    public List<string> DeletedTables { get; } = [];
    public List<TableOptionsUpdateRequest> UpdatedTableOptions { get; } = [];
    public List<string> CreatedWorksheets { get; } = [];
    public List<(string SheetName, string NewSheetName)> RenamedWorksheets { get; } = [];
    public List<string> DeletedWorksheets { get; } = [];
    public List<WorksheetMoveRequest> MovedWorksheets { get; } = [];
    public List<WorksheetCopyRequest> CopiedWorksheets { get; } = [];
    public List<WorksheetVisibilityRequest> WorksheetVisibilityChanges { get; } = [];

    public Func<string, Task<QueryDefinition>> OnGetQueryAsync { get; set; } =
        name => Task.FromResult(new QueryDefinition(name, "let Source = 1 in Source"));

    public Func<string, Task<QueryDetail>> OnGetQueryDetailAsync { get; set; } =
        name => Task.FromResult(new QueryDetail(name, "let Source = 1 in Source", null, QueryLoadModes.None, null, null, null, $"query:{name}"));

    public Func<QueryCreateRequest, Task> OnCreateQueryAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<QueryRenameRequest, Task> OnRenameQueryAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, Task> OnDeleteQueryAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, Task<ConnectionDetail>> OnGetConnectionAsync { get; set; } =
        name => Task.FromResult(new ConnectionDetail(name, "2", true, null, null, null, null, null, Array.Empty<string>(), $"connection:{name}"));

    public Func<ConnectionRenameRequest, Task> OnRenameConnectionAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<ConnectionUpdateRequest, Task> OnUpdateConnectionAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, Task> OnDeleteConnectionAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<Task<WorkbookDependencyGraph>> OnGetDependencyGraphAsync { get; set; } =
        () => Task.FromResult(new WorkbookDependencyGraph(Array.Empty<WorkbookDependencyNode>(), Array.Empty<WorkbookDependencyEdge>()));

    public Func<Task<WorkbookStructureState>> OnGetWorkbookStructureStateAsync { get; set; } =
        () => Task.FromResult(new WorkbookStructureState(WorkbookVisibilityModes.Visible, new WorkbookProtectionState(false, false, false)));

    public Func<Task<WorkbookProtectionState>> OnGetWorkbookProtectionStateAsync { get; set; } =
        () => Task.FromResult(new WorkbookProtectionState(false, false, false));

    public Func<WorkbookVisibilityRequest, Task> OnSetWorkbookVisibilityAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<WorkbookProtectionUpdateRequest, Task> OnSetWorkbookProtectionAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, string?, Task<NameSummary>> OnGetNameAsync { get; set; } =
        (name, sheetName) => Task.FromResult(new NameSummary(name, sheetName is null ? "Workbook" : "Worksheet", sheetName, "=Sheet1!$A$1", "$A$1"));

    public Func<string, string, string?, Task> OnCreateNameAsync { get; set; } =
        (name, refersTo, sheetName) => Task.CompletedTask;

    public Func<string, string, string?, Task> OnUpdateNameAsync { get; set; } =
        (name, refersTo, sheetName) => Task.CompletedTask;

    public Func<string, string?, Task> OnDeleteNameAsync { get; set; } =
        (name, sheetName) => Task.CompletedTask;

    public Func<QueryProbeRequest, Task<ProbeResult>> OnRunProbeAsync { get; set; } =
        request => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));

    public Func<string, Task<CleanupResult>> OnCleanupAsync { get; set; } =
        pattern => Task.FromResult(new CleanupResult(0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<OperationError>()));

    public Func<string, RefreshOptions?, Task<RefreshResult>> OnRefreshAsync { get; set; } =
        (queryName, options) => Task.FromResult(new RefreshResult(true, queryName, "query", TimeSpan.Zero));

    public Func<string, string?, Task<RangeData>> OnReadRangeAsync { get; set; } =
        (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { "value" } }));

    public Func<string, string?, Task<RangeData>> OnReadRangeFormulasAsync { get; set; } =
        (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { "=1+1" } }));

    public Func<string, string?, Task<RangeFormatData>> OnReadRangeFormatAsync { get; set; } =
        (address, sheetName) => Task.FromResult(new RangeFormatData(sheetName ?? "Sheet1", address, new RangeFormatSnapshot(Bold: true, HasFill: true, FillColor: "#FFFFFF"), Array.Empty<string>()));

    public Func<string, string?, Task<RangeData>> OnReadNamedRangeAsync { get; set; } =
        (name, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", "$A$1", new object?[,] { { "value" } }));

    public Func<string, Task<TableReadResult>> OnReadTableAsync { get; set; } =
        tableName => Task.FromResult(new TableReadResult(tableName, "Sheet1", "$A$1:$B$2", ["Column1", "Column2"], [[1d, 2d]], false));

    public Func<string, Task<TableDetailResult>> OnGetTableAsync { get; set; } =
        tableName => Task.FromResult(new TableDetailResult(tableName, "Sheet1", "$A$1:$B$2", ["Column1", "Column2"], 1, 2, true, false, false, null));

    public Func<TableCreateRequest, Task> OnCreateTableAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<TableResizeRequest, Task> OnResizeTableAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<TableRowsWriteRequest, Task> OnAppendTableRowsAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<TableRowsWriteRequest, Task> OnReplaceTableRowsAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, Task> OnDeleteTableAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<TableOptionsUpdateRequest, Task> OnSetTableOptionsAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, Task> OnCreateWorksheetAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, string, Task> OnRenameWorksheetAsync { get; set; } =
        (_, _) => Task.CompletedTask;

    public Func<string, Task> OnDeleteWorksheetAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<WorksheetMoveRequest, Task> OnMoveWorksheetAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<WorksheetCopyRequest, Task> OnCopyWorksheetAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<WorksheetVisibilityRequest, Task> OnSetWorksheetVisibilityAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<string, object?[,], string?, Task> OnWriteRangeAsync { get; set; } =
        (address, values, sheetName) => Task.CompletedTask;

    public Func<string, string?[,], string?, Task> OnWriteRangeFormulasAsync { get; set; } =
        (address, formulas, sheetName) => Task.CompletedTask;

    public Func<string, RangeFormatPatch, string?, Task> OnWriteRangeFormatAsync { get; set; } =
        (address, format, sheetName) => Task.CompletedTask;

    public Func<string, string, string?, Task> OnAutofitRangeAsync { get; set; } =
        (address, dimension, sheetName) => Task.CompletedTask;

    public Func<string, string?, Task> OnClearRangeContentsAsync { get; set; } =
        (address, sheetName) => Task.CompletedTask;

    public Func<CalculationRequest, Task> OnRecalculateAsync { get; set; } =
        _ => Task.CompletedTask;

    public Func<ErrorInspectionRequest, Task<IReadOnlyList<ErrorInspectionHit>>> OnInspectErrorsAsync { get; set; } =
        _ => Task.FromResult<IReadOnlyList<ErrorInspectionHit>>(Array.Empty<ErrorInspectionHit>());

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
    public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => OnGetQueryAsync(queryName);
    public Task<QueryDetail> GetQueryDetailAsync(string queryName, CancellationToken cancellationToken = default) => OnGetQueryDetailAsync(queryName);
    public Task CreateQueryAsync(QueryCreateRequest request, CancellationToken cancellationToken = default)
    {
        CreatedQueries.Add(request);
        return OnCreateQueryAsync(request);
    }

    public Task RenameQueryAsync(QueryRenameRequest request, CancellationToken cancellationToken = default)
    {
        RenamedQueries.Add(request);
        return OnRenameQueryAsync(request);
    }

    public Task DeleteQueryAsync(string queryName, CancellationToken cancellationToken = default)
    {
        DeletedQueries.Add(queryName);
        return OnDeleteQueryAsync(queryName);
    }

    public Task<ConnectionDetail> GetConnectionAsync(string connectionName, CancellationToken cancellationToken = default) => OnGetConnectionAsync(connectionName);

    public Task RenameConnectionAsync(ConnectionRenameRequest request, CancellationToken cancellationToken = default)
    {
        RenamedConnections.Add(request);
        return OnRenameConnectionAsync(request);
    }

    public Task UpdateConnectionAsync(ConnectionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        UpdatedConnections.Add(request);
        return OnUpdateConnectionAsync(request);
    }

    public Task DeleteConnectionAsync(string connectionName, CancellationToken cancellationToken = default)
    {
        DeletedConnections.Add(connectionName);
        return OnDeleteConnectionAsync(connectionName);
    }

    public Task<WorkbookDependencyGraph> GetDependencyGraphAsync(CancellationToken cancellationToken = default) => OnGetDependencyGraphAsync();

    public Task<WorkbookStructureState> GetWorkbookStructureStateAsync(CancellationToken cancellationToken = default) => OnGetWorkbookStructureStateAsync();

    public Task<WorkbookProtectionState> GetWorkbookProtectionStateAsync(CancellationToken cancellationToken = default) => OnGetWorkbookProtectionStateAsync();

    public Task SetWorkbookVisibilityAsync(WorkbookVisibilityRequest request, CancellationToken cancellationToken = default)
    {
        WorkbookVisibilityChanges.Add(request);
        return OnSetWorkbookVisibilityAsync(request);
    }

    public Task SetWorkbookProtectionAsync(WorkbookProtectionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        WorkbookProtectionChanges.Add(request);
        return OnSetWorkbookProtectionAsync(request);
    }

    public Task<NameSummary> GetNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) => OnGetNameAsync(name, sheetName);
    public Task CreateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        CreatedNames.Add((name, refersTo, sheetName));
        return OnCreateNameAsync(name, refersTo, sheetName);
    }

    public Task UpdateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        UpdatedNames.Add((name, refersTo, sheetName));
        return OnUpdateNameAsync(name, refersTo, sheetName);
    }

    public Task DeleteNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        DeletedNames.Add((name, sheetName));
        return OnDeleteNameAsync(name, sheetName);
    }
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

    public Task<RangeData> ReadRangeFormulasAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ReadRangeFormulaCalls.Add((sheetName ?? "Sheet1", address));
        return OnReadRangeFormulasAsync(address, sheetName);
    }

    public Task<RangeFormatData> ReadRangeFormatAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ReadRangeFormatCalls.Add((sheetName ?? "Sheet1", address));
        return OnReadRangeFormatAsync(address, sheetName);
    }

    public Task<RangeData> ReadNamedRangeAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) =>
        OnReadNamedRangeAsync(name, sheetName);

    public Task<TableReadResult> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        OnReadTableAsync(tableName);

    public Task<TableDetailResult> GetTableAsync(string tableName, CancellationToken cancellationToken = default) =>
        OnGetTableAsync(tableName);

    public Task CreateTableAsync(TableCreateRequest request, CancellationToken cancellationToken = default)
    {
        CreatedTables.Add(request);
        return OnCreateTableAsync(request);
    }

    public Task ResizeTableAsync(TableResizeRequest request, CancellationToken cancellationToken = default)
    {
        ResizedTables.Add(request);
        return OnResizeTableAsync(request);
    }

    public Task AppendTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default)
    {
        AppendedTableRows.Add(request);
        return OnAppendTableRowsAsync(request);
    }

    public Task ReplaceTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default)
    {
        ReplacedTableRows.Add(request);
        return OnReplaceTableRowsAsync(request);
    }

    public Task DeleteTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        DeletedTables.Add(tableName);
        return OnDeleteTableAsync(tableName);
    }

    public Task SetTableOptionsAsync(TableOptionsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        UpdatedTableOptions.Add(request);
        return OnSetTableOptionsAsync(request);
    }

    public Task RecalculateAsync(CalculationRequest request, CancellationToken cancellationToken = default)
    {
        RecalculationCalls.Add(request);
        return OnRecalculateAsync(request);
    }

    public Task<IReadOnlyList<ErrorInspectionHit>> InspectErrorsAsync(ErrorInspectionRequest request, CancellationToken cancellationToken = default)
    {
        ErrorInspectionCalls.Add(request);
        return OnInspectErrorsAsync(request);
    }

    public Task CreateWorksheetAsync(string sheetName, CancellationToken cancellationToken = default)
    {
        CreatedWorksheets.Add(sheetName);
        return OnCreateWorksheetAsync(sheetName);
    }

    public Task RenameWorksheetAsync(string sheetName, string newSheetName, CancellationToken cancellationToken = default)
    {
        RenamedWorksheets.Add((sheetName, newSheetName));
        return OnRenameWorksheetAsync(sheetName, newSheetName);
    }

    public Task DeleteWorksheetAsync(string sheetName, CancellationToken cancellationToken = default)
    {
        DeletedWorksheets.Add(sheetName);
        return OnDeleteWorksheetAsync(sheetName);
    }

    public Task MoveWorksheetAsync(WorksheetMoveRequest request, CancellationToken cancellationToken = default)
    {
        MovedWorksheets.Add(request);
        return OnMoveWorksheetAsync(request);
    }

    public Task CopyWorksheetAsync(WorksheetCopyRequest request, CancellationToken cancellationToken = default)
    {
        CopiedWorksheets.Add(request);
        return OnCopyWorksheetAsync(request);
    }

    public Task SetWorksheetVisibilityAsync(WorksheetVisibilityRequest request, CancellationToken cancellationToken = default)
    {
        WorksheetVisibilityChanges.Add(request);
        return OnSetWorksheetVisibilityAsync(request);
    }

    public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeCalls.Add((sheetName ?? "Sheet1", address, values));
        return OnWriteRangeAsync(address, values, sheetName);
    }

    public Task WriteRangeFormulasAsync(string address, string?[,] formulas, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeFormulaCalls.Add((sheetName ?? "Sheet1", address, formulas));
        return OnWriteRangeFormulasAsync(address, formulas, sheetName);
    }

    public Task WriteRangeFormatAsync(string address, RangeFormatPatch format, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        WriteRangeFormatCalls.Add((sheetName ?? "Sheet1", address, format));
        return OnWriteRangeFormatAsync(address, format, sheetName);
    }

    public Task AutofitRangeAsync(string address, string dimension, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        AutofitRangeCalls.Add((sheetName ?? "Sheet1", address, dimension));
        return OnAutofitRangeAsync(address, dimension, sheetName);
    }

    public Task ClearRangeContentsAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default)
    {
        ClearRangeCalls.Add((sheetName ?? "Sheet1", address));
        return OnClearRangeContentsAsync(address, sheetName);
    }
}
