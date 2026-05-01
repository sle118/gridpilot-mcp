using ExcelMcp.Bridge.Services;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;
using ExcelMcp.UnitTests.Fakes;

namespace ExcelMcp.UnitTests.Services;

public sealed class WorkbookServiceTests
{
    [Fact]
    public async Task InventoryMethods_ReturnDataFromWorkbookHandle()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Sheets = [new SheetSummary("Sheet1", "Worksheet", true)],
            Tables = [new TableSummary("Sheet1", "SalesTable", "$A$1:$D$12", true, "SalesQuery")],
            Queries = [new QuerySummary("SalesQuery", true, false, "let Source = 1 in Source")],
            Connections = [new ConnectionSummary("Query - SalesQuery", "2", true)]
        };

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var sheets = await sut.ListSheetsAsync("C:/temp/book.xlsx");
        var tables = await sut.ListTablesAsync("C:/temp/book.xlsx");
        var queries = await sut.ListQueriesAsync("C:/temp/book.xlsx");
        var connections = await sut.ListConnectionsAsync("C:/temp/book.xlsx");

        Assert.Equal(fakeWorkbook.Sheets, sheets);
        Assert.Equal(fakeWorkbook.Tables, tables);
        Assert.Equal(fakeWorkbook.Queries, queries);
        Assert.Equal(fakeWorkbook.Connections, connections);
    }

    [Fact]
    public async Task ListInventoryAsync_AggregatesWorkbookInventory()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Sheets = [new SheetSummary("Sheet1", "Worksheet", true)],
            Tables = [new TableSummary("Sheet1", "SalesTable", "$A$1:$D$12", true, "SalesQuery")],
            Queries = [new QuerySummary("SalesQuery", true, false, "let Source = 1 in Source")],
            Connections = [new ConnectionSummary("Query - SalesQuery", "2", true)]
        };

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var inventory = await sut.ListInventoryAsync("C:/temp/book.xlsx");

        Assert.Equal(fakeWorkbook.Sheets, inventory.Sheets);
        Assert.Equal(fakeWorkbook.Tables, inventory.Tables);
        Assert.Equal(fakeWorkbook.Queries, inventory.Queries);
        Assert.Equal(fakeWorkbook.Connections, inventory.Connections);
    }

    [Fact]
    public async Task NameMethods_ReturnDataFromWorkbookHandle()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Names =
            [
                new NameSummary("SalesRange", "Workbook", null, "=Sheet1!$A$1:$B$2", "$A$1:$B$2")
            ],
            OnGetNameAsync = (_, _) => Task.FromResult(new NameSummary("SalesRange", "Workbook", null, "=Sheet1!$A$1:$B$2", "$A$1:$B$2"))
        };

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var names = await sut.ListNamesAsync("C:/temp/book.xlsx");
        var name = await sut.GetNameAsync("C:/temp/book.xlsx", "SalesRange");

        Assert.Single(names);
        Assert.Equal("SalesRange", names[0].Name);
        Assert.Equal("SalesRange", name.Name);
        Assert.Equal("$A$1:$B$2", name.Address);
    }

    [Fact]
    public async Task ReadNamedRangeAsync_ReturnsConvertedValues()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnReadNamedRangeAsync = (_, _) =>
            Task.FromResult(new RangeData("Sheet1", "$C$1:$D$2", new object?[,] { { "left", "right" }, { 10d, 20d } }));

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.ReadNamedRangeAsync("C:/temp/book.xlsx", "SalesRange");

        Assert.Equal("Sheet1", result.SheetName);
        Assert.Equal("$C$1:$D$2", result.Address);
        Assert.Equal("left", result.Values[0][0]);
        Assert.Equal(20d, result.Values[1][1]);
    }

    [Fact]
    public async Task ReadTableAsync_ReturnsHeadersAndRows()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnReadTableAsync = tableName =>
            Task.FromResult(new TableReadResult(
                tableName,
                "Sheet1",
                "$A$1:$B$3",
                ["First", "Second"],
                [[1d, 2d], [3d, 4d]],
                false));

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.ReadTableAsync("C:/temp/book.xlsx", "SalesTable");

        Assert.Equal("SalesTable", result.TableName);
        Assert.Equal("Sheet1", result.SheetName);
        Assert.Equal("First", result.Headers[0]);
        Assert.Equal(4d, result.Rows[1][1]);
    }

    [Fact]
    public async Task GetTableAsync_ReturnsStructuredMetadata()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnGetTableAsync = tableName =>
            Task.FromResult(new TableDetailResult(
                tableName,
                "Sheet1",
                "$A$1:$B$3",
                ["First", "Second"],
                2,
                2,
                true,
                false,
                true,
                "SalesQuery"));

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.GetTableAsync("C:/temp/book.xlsx", "SalesTable");

        Assert.Equal("SalesTable", result.TableName);
        Assert.Equal(2, result.RowCount);
        Assert.True(result.IsQueryBacked);
        Assert.Equal("SalesQuery", result.QueryName);
    }

    [Fact]
    public async Task CreateTableAsync_SavesWorkbookOnSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.CreateTableAsync(
            @"C:\temp\book.xlsx",
            new TableCreateRequest("GridPilotTable", "Sheet1", "Z1:AA3"));

        Assert.True(result.Succeeded);
        var created = Assert.Single(fakeWorkbook.CreatedTables);
        Assert.Equal("GridPilotTable", created.TableName);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task AppendTableRowsAsync_RequiresApprovalInAttachedMode()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, new InMemoryAttachedMutationApprovalRegistry()));

        var result = await sut.AppendTableRowsAsync(
            @"C:\temp\book.xlsx",
            new TableRowsWriteRequest("SalesTable", new object?[,] { { "A", "B" } }));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_approval_required", result.Error?.Code);
        Assert.Empty(fakeWorkbook.AppendedTableRows);
    }

    [Fact]
    public async Task AppendTableRowsAsync_AllowsAttachedMutationWithApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var registry = new InMemoryAttachedMutationApprovalRegistry();
        registry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, registry));

        var result = await sut.AppendTableRowsAsync(
            @"C:\temp\book.xlsx",
            new TableRowsWriteRequest("SalesTable", new object?[,] { { "A", "B" } }));

        Assert.True(result.Succeeded);
        Assert.Single(fakeWorkbook.AppendedTableRows);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task ReplaceTableRowsAsync_FailsWhenColumnCountDoesNotMatchTable()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnGetTableAsync = tableName =>
            Task.FromResult(new TableDetailResult(tableName, "Sheet1", "$A$1:$B$2", ["A", "B"], 1, 2, true, false, false, null));
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.ReplaceTableRowsAsync(
            @"C:\temp\book.xlsx",
            new TableRowsWriteRequest("SalesTable", new object?[,] { { "A" } }));

        Assert.False(result.Succeeded);
        Assert.Equal("table_replace_rows_failed", result.Error?.Code);
        Assert.Empty(fakeWorkbook.ReplacedTableRows);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task ResizeTableAsync_DoesNotSaveOnFailure()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnResizeTableAsync = _ => throw new InvalidOperationException("boom");
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.ResizeTableAsync(
            @"C:\temp\book.xlsx",
            new TableResizeRequest("SalesTable", "Sheet1", "A1:B5"));

        Assert.False(result.Succeeded);
        Assert.Equal("table_resize_failed", result.Error?.Code);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task SetTableOptionsAsync_BlocksForUnsafeUiEvenWithApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, false, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner, IsEditingCell: true)
        };
        var registry = new InMemoryAttachedMutationApprovalRegistry();
        registry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, registry));

        var result = await sut.SetTableOptionsAsync(
            @"C:\temp\book.xlsx",
            new TableOptionsUpdateRequest("SalesTable", ShowTotals: true));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_ui_unsafe", result.Error?.Code);
        Assert.Empty(fakeWorkbook.UpdatedTableOptions);
    }

    [Fact]
    public async Task CreateNameAsync_SavesWorkbookOnSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.CreateNameAsync("C:/temp/book.xlsx", "SalesRange", "=Sheet1!$A$1:$B$2");

        Assert.True(result.Succeeded);
        var created = Assert.Single(fakeWorkbook.CreatedNames);
        Assert.Equal("SalesRange", created.Name);
        Assert.Equal("=Sheet1!$A$1:$B$2", created.RefersTo);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task UpdateNameAsync_AllowsWorksheetScopedNameMutationWithApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var registry = new InMemoryAttachedMutationApprovalRegistry();
        registry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, registry));

        var result = await sut.UpdateNameAsync(@"C:\temp\book.xlsx", "LocalRange", "=Sheet1!$C$1:$C$2", "Sheet1");

        Assert.True(result.Succeeded);
        var updated = Assert.Single(fakeWorkbook.UpdatedNames);
        Assert.Equal("LocalRange", updated.Name);
        Assert.Equal("Sheet1", updated.SheetName);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task DeleteNameAsync_RequiresApprovalForAttachedMutation()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, new InMemoryAttachedMutationApprovalRegistry()));

        var result = await sut.DeleteNameAsync(@"C:\temp\book.xlsx", "SalesRange");

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_approval_required", result.Error?.Code);
        Assert.Empty(fakeWorkbook.DeletedNames);
    }

    [Fact]
    public async Task UpdateNameAsync_DoesNotSaveOnFailure()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnUpdateNameAsync = (_, _, _) => throw new InvalidOperationException("boom");
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.UpdateNameAsync(@"C:\temp\book.xlsx", "SalesRange", "=Sheet1!$A$1");

        Assert.False(result.Succeeded);
        Assert.Equal("name_update_failed", result.Error?.Code);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task ListInventoryAsync_AllowsReadOnlyAccessInAttachedSession()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Queries = [new QuerySummary("SalesQuery", true, false, "let Source = 1 in Source")]
        };

        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, false, false, ExcelCalculationState.Pending, SessionAttachTargetMode.WorkbookOwner),
            OpenWorkbooks = [new WorkbookSummary("book.xlsx", @"C:\temp\book.xlsx", true)]
        };

        var sut = new WorkbookService(session);

        var inventory = await sut.ListInventoryAsync(@"C:\temp\book.xlsx");

        Assert.Single(inventory.Queries);
        Assert.Empty(session.PushedOptions);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task TryRunQueryAsync_UsesGeneratedTempNameWithPrefix()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        QueryProbeRequest? captured = null;
        fakeWorkbook.OnRunProbeAsync = request =>
        {
            captured = request;
            return Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));
        };

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.TryRunQueryAsync("C:/temp/book.xlsx", "SalesQuery", "tmp_probe");

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal("SalesQuery", captured!.TargetQueryName);
        Assert.StartsWith("tmp_probe_SalesQuery_", captured.TempQueryName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanupTempQueriesAsync_SavesWorkbookWhenQueriesWereDeleted()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnCleanupAsync = _ => Task.FromResult(new CleanupResult(
            DeletedCount: 2,
            DeletedNames: ["tmp_probe_one", "tmp_probe_two"],
            FailedNames: Array.Empty<string>(),
            Errors: Array.Empty<OperationError>()));

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.CleanupTempQueriesAsync("C:/temp/book.xlsx", "tmp_probe_");

        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task RefreshQueryAsync_ForwardsOptionsToWorkbookHandle()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);
        var options = new RefreshOptions(Silent: false, PreferSynchronousTableRefresh: false, Timeout: TimeSpan.FromSeconds(5));

        var result = await sut.RefreshQueryAsync("C:/temp/book.xlsx", "SalesQuery", options);

        Assert.True(result.Succeeded);
        var call = Assert.Single(fakeWorkbook.RefreshCalls);
        Assert.Equal("SalesQuery", call.QueryName);
        Assert.Equal(options, call.Options);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
        Assert.Empty(session.PushedOptions);
    }

    [Fact]
    public async Task RefreshQueryAsync_UsesQuietSessionScopeWhenSilent()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        await sut.RefreshQueryAsync("C:/temp/book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        var scope = Assert.Single(session.PushedOptions);
        Assert.False(scope.DisplayAlerts);
        Assert.False(scope.ScreenUpdating);
        Assert.False(scope.EnableEvents);
        Assert.Equal(1, session.PopCallCount);
    }

    [Fact]
    public async Task RefreshQueryAsync_RequiresApprovalForAttachedWorkbookOwnerMutation()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal("shared_session_approval_required", result.Error!.Code);
        Assert.Empty(fakeWorkbook.RefreshCalls);
        Assert.Empty(session.PushedOptions);
    }

    [Fact]
    public async Task RefreshQueryAsync_BlocksForUnsafeUiStateWhenExcelIsNotReady()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.CreateNew, false, true, ExcelCalculationState.Done)
        };
        var sut = new WorkbookService(session);

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_ui_unsafe", result.Error?.Code);
        Assert.Empty(fakeWorkbook.RefreshCalls);
    }

    [Fact]
    public async Task RefreshQueryAsync_BlocksWhenExcelAppearsToBeInCellEditMode()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.CreateNew, false, true, ExcelCalculationState.Done, null, IsEditingCell: true)
        };
        var sut = new WorkbookService(session);

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_ui_unsafe", result.Error?.Code);
        Assert.Contains("active cell edit mode", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshQueryAsync_BlocksWhenExcelAppearsToHaveModalUi()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.CreateNew, true, false, ExcelCalculationState.Done, null, HasModalUi: true)
        };
        var sut = new WorkbookService(session);

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_ui_unsafe", result.Error?.Code);
        Assert.Contains("modal ui", result.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshQueryAsync_BlocksForBusyCalculationState()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.CreateNew, true, true, ExcelCalculationState.Calculating)
        };
        var sut = new WorkbookService(session);

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_busy", result.Error?.Code);
        Assert.Empty(fakeWorkbook.RefreshCalls);
    }

    [Fact]
    public async Task RefreshQueryAsync_AllowsAttachedMutationWithApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        approvalRegistry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Single(fakeWorkbook.RefreshCalls);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task RefreshQueryAsync_AllowsAttachedMutationWithUrlStyleWorkbookApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        approvalRegistry.Grant("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.RefreshQueryAsync("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Single(fakeWorkbook.RefreshCalls);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task RefreshQueryAsync_ReturnsExpiredApprovalError()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var now = new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);
        var clockNow = now;
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry(() => clockNow);
        approvalRegistry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        clockNow = now.AddMinutes(11);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_approval_expired", result.Error?.Code);
        Assert.Empty(fakeWorkbook.RefreshCalls);
    }

    [Fact]
    public async Task RefreshQueryAsync_ReturnsScopeMismatchWhenApprovalExistsForDifferentWorkbook()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        approvalRegistry.Grant(@"C:\temp\other.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.RefreshQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", new RefreshOptions(Silent: true));

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_approval_scope_mismatch", result.Error?.Code);
        Assert.Empty(fakeWorkbook.RefreshCalls);
    }

    [Fact]
    public async Task RefreshQueryAsync_DoesNotSaveWorkbookWhenRefreshFails()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnRefreshAsync = (queryName, options) => Task.FromResult(new RefreshResult(false, queryName, "connection", TimeSpan.Zero, new OperationError("query_refresh_failed", "failed")))
        };
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.RefreshQueryAsync("C:/temp/book.xlsx", "SalesQuery", new RefreshOptions(Silent: false));

        Assert.False(result.Succeeded);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task TryRunQueryAsync_UsesQuietSessionScope()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        await sut.TryRunQueryAsync("C:/temp/book.xlsx", "SalesQuery", "tmp_probe");

        var scope = Assert.Single(session.PushedOptions);
        Assert.False(scope.DisplayAlerts);
        Assert.False(scope.ScreenUpdating);
        Assert.False(scope.EnableEvents);
        Assert.Equal(1, session.PopCallCount);
    }

    [Fact]
    public async Task TryRunQueryAsync_RequiresApprovalForAttachedWorkbookOwnerMutation()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.TryRunQueryAsync(@"C:\temp\book.xlsx", "SalesQuery", "tmp_probe");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal("shared_session_approval_required", result.Error!.Code);
        Assert.Empty(session.PushedOptions);
    }

    [Fact]
    public async Task CleanupTempQueriesAsync_RequiresApprovalForAttachedWorkbookOwnerMutation()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.CleanupTempQueriesAsync(@"C:\temp\book.xlsx", "tmp_probe_");

        Assert.Equal(0, result.DeletedCount);
        var errors = result.Errors;
        Assert.NotNull(errors);
        Assert.Single(errors);
        Assert.Equal("shared_session_approval_required", errors[0].Code);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task CleanupTempQueriesAsync_AllowsAttachedMutationWithApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnCleanupAsync = _ => Task.FromResult(new CleanupResult(
            DeletedCount: 1,
            DeletedNames: ["tmp_probe_one"],
            FailedNames: Array.Empty<string>(),
            Errors: Array.Empty<OperationError>()));

        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        approvalRegistry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, approvalRegistry));

        var result = await sut.CleanupTempQueriesAsync(@"C:\temp\book.xlsx", "tmp_probe_");

        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task SetQueryFormulaAsync_SavesWorkbookOnSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.SetQueryFormulaAsync(@"C:\temp\book.xlsx", "SalesQuery", "let Source = 1 in Source");

        Assert.True(result.Succeeded);
        Assert.Equal("SalesQuery", result.QueryName);
        var call = Assert.Single(fakeWorkbook.SetQueryFormulaCalls);
        Assert.Equal("SalesQuery", call.QueryName);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task SetQueryFormulaAsync_RequiresApprovalInAttachedMode()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, new InMemoryAttachedMutationApprovalRegistry()));

        var result = await sut.SetQueryFormulaAsync(@"C:\temp\book.xlsx", "SalesQuery", "let Source = 1 in Source");

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_approval_required", result.Error?.Code);
        Assert.Empty(fakeWorkbook.SetQueryFormulaCalls);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task SetQueryFormulaAsync_AllowsAttachedMutationWithApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var registry = new InMemoryAttachedMutationApprovalRegistry();
        registry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, registry));

        var result = await sut.SetQueryFormulaAsync(@"C:\temp\book.xlsx", "SalesQuery", "let Source = 1 in Source");

        Assert.True(result.Succeeded);
        Assert.Single(fakeWorkbook.SetQueryFormulaCalls);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task AttachedWorkbookApprovalLease_AllowsMultipleMutationFamiliesForSameWorkbook()
    {
        var workbookPath = "https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx";
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnReadRangeAsync = (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { null } }))
        };
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var registry = new InMemoryAttachedMutationApprovalRegistry();
        registry.Grant(workbookPath, TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, registry));

        var refreshResult = await sut.RefreshQueryAsync(workbookPath, "SalesQuery", new RefreshOptions(Silent: true));
        var formulaResult = await sut.SetQueryFormulaAsync(workbookPath, "SalesQuery", "let Source = 1 in Source");
        var writeResult = await sut.WriteRangesAsync(
            workbookPath,
            new RangeWriteRequest([new RangeWriteTarget("Sheet1", "A1", new object?[,] { { "A" } })]));

        Assert.True(refreshResult.Succeeded);
        Assert.True(formulaResult.Succeeded);
        Assert.True(writeResult.Succeeded);
        Assert.Single(fakeWorkbook.RefreshCalls);
        Assert.Single(fakeWorkbook.SetQueryFormulaCalls);
        Assert.Single(fakeWorkbook.WriteRangeCalls);
        Assert.Equal(3, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task SetQueryFormulaAsync_DoesNotSaveOnFailure()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnReadRangeAsync = (address, sheet) => Task.FromResult(new RangeData(sheet ?? "Sheet1", address, new object?[,] { { "value" } }))
        };
        fakeWorkbook.OnWriteRangeAsync = (_, _, _) => Task.CompletedTask;
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        fakeWorkbook.SetQueryFormulaCalls.Clear();
        var sut = new WorkbookService(session);
        fakeWorkbook.OnGetQueryAsync = _ => Task.FromResult(new QueryDefinition("SalesQuery", ""));
        fakeWorkbook.OnWriteRangeAsync = (_, _, _) => Task.CompletedTask;
        fakeWorkbook.OnReadRangeAsync = (address, sheet) => Task.FromResult(new RangeData(sheet ?? "Sheet1", address, new object?[,] { { "value" } }));
        fakeWorkbook.SetQueryFormulaCalls.Clear();
        fakeWorkbook.OnGetQueryAsync = _ => throw new InvalidOperationException("unused");

        fakeWorkbook.SetQueryFormulaCalls.Clear();
        fakeWorkbook.OnReadRangeAsync = (address, sheet) => Task.FromResult(new RangeData(sheet ?? "Sheet1", address, new object?[,] { { "value" } }));

        // Simulate a workbook-side formula update failure.
        var failingWorkbook = new FakeWorkbookHandle();
        failingWorkbook.OnReadRangeAsync = fakeWorkbook.OnReadRangeAsync;
        failingWorkbook.OnWriteRangeAsync = fakeWorkbook.OnWriteRangeAsync;
        var failingSession = new FakeExcelSession { Workbook = failingWorkbook };
        var failingService = new WorkbookService(failingSession);
        failingWorkbook.OnCleanupAsync = fakeWorkbook.OnCleanupAsync;
        failingWorkbook.OnRunProbeAsync = fakeWorkbook.OnRunProbeAsync;
        failingWorkbook.OnRefreshAsync = fakeWorkbook.OnRefreshAsync;
        failingWorkbook.OnGetQueryAsync = fakeWorkbook.OnGetQueryAsync;
        failingWorkbook.SetQueryFormulaCalls.Clear();
        failingWorkbook.OnWriteRangeAsync = (_, _, _) => Task.CompletedTask;
        failingWorkbook.OnReadRangeAsync = (address, sheet) => Task.FromResult(new RangeData(sheet ?? "Sheet1", address, new object?[,] { { "value" } }));
        // Force failure via a derived delegate path.
        failingWorkbook.OnGetQueryAsync = _ => Task.FromResult(new QueryDefinition("SalesQuery", ""));
        failingWorkbook.SetQueryFormulaCalls.Clear();
        failingWorkbook.OnReadRangeAsync = fakeWorkbook.OnReadRangeAsync;
        failingWorkbook.OnWriteRangeAsync = fakeWorkbook.OnWriteRangeAsync;

        // Replace workbook with one that throws on formula set by using the delegate-less fake and wrapping the service call.
        var throwingWorkbook = new ThrowingFormulaWorkbookHandle();
        var throwingSession = new FakeExcelSession { Workbook = throwingWorkbook };
        var throwingService = new WorkbookService(throwingSession);

        var result = await throwingService.SetQueryFormulaAsync(@"C:\temp\book.xlsx", "SalesQuery", "let Source = 1 in Source");

        Assert.False(result.Succeeded);
        Assert.Equal("query_formula_update_failed", result.Error?.Code);
        Assert.Equal(0, throwingWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task ReadRangeAsync_ReturnsExpectedPayloadWithoutApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnReadRangeAsync = (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { 1, 2 }, { 3, 4 } }))
        };
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, new InMemoryAttachedMutationApprovalRegistry()));

        var result = await sut.ReadRangeAsync(@"C:\temp\book.xlsx", "Sheet1", "A1:B2");

        Assert.Equal("Sheet1", result.SheetName);
        Assert.Equal("A1:B2", result.Address);
        Assert.Equal(2, result.Values.Count);
        Assert.Equal(4L, Convert.ToInt64(result.Values[1][1]));
    }

    [Fact]
    public async Task WriteRangesAsync_SavesWorkbookOnSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnReadRangeAsync = (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { null, null } }))
        };
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);
        var request = new RangeWriteRequest(
        [
            new RangeWriteTarget("Sheet1", "A1:B1", new object?[,] { { "A", "B" } }),
            new RangeWriteTarget("Sheet1", "A2:B2", new object?[,] { { "C", "D" } })
        ]);

        var result = await sut.WriteRangesAsync(@"C:\temp\book.xlsx", request);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.WriteCount);
        Assert.Equal(2, fakeWorkbook.WriteRangeCalls.Count);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task WriteRangesAsync_RequiresApprovalInAttachedMode()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnReadRangeAsync = (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { null } }))
        };
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, new InMemoryAttachedMutationApprovalRegistry()));
        var request = new RangeWriteRequest([new RangeWriteTarget("Sheet1", "A1", new object?[,] { { "A" } })]);

        var result = await sut.WriteRangesAsync(@"C:\temp\book.xlsx", request);

        Assert.False(result.Succeeded);
        Assert.Equal("shared_session_approval_required", result.Error?.Code);
        Assert.Empty(fakeWorkbook.WriteRangeCalls);
    }

    [Fact]
    public async Task WriteRangesAsync_AllowsAttachedMutationWithApproval()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnReadRangeAsync = (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { null } }))
        };
        var session = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var registry = new InMemoryAttachedMutationApprovalRegistry();
        registry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(10), out _);
        var sut = new WorkbookService(session, new WorkbookOperationSafety(session, registry));
        var request = new RangeWriteRequest([new RangeWriteTarget("Sheet1", "A1", new object?[,] { { "A" } })]);

        var result = await sut.WriteRangesAsync(@"C:\temp\book.xlsx", request);

        Assert.True(result.Succeeded);
        Assert.Single(fakeWorkbook.WriteRangeCalls);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task WriteRangesAsync_FailsPreflightWithoutApplyingAnyWrites()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            OnReadRangeAsync = (address, sheetName) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { null, null } }))
        };
        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);
        var request = new RangeWriteRequest([new RangeWriteTarget("Sheet1", "A1:B1", new object?[,] { { "A" } })]);

        var result = await sut.WriteRangesAsync(@"C:\temp\book.xlsx", request);

        Assert.False(result.Succeeded);
        Assert.Equal("range_write_failed", result.Error?.Code);
        Assert.Empty(fakeWorkbook.WriteRangeCalls);
        Assert.Equal(0, fakeWorkbook.SaveCallCount);
    }

    private sealed class ThrowingFormulaWorkbookHandle : IWorkbookHandle
    {
        public string Name => "fake.xlsx";
        public string FullPath => @"C:\temp\fake.xlsx";
        public int SaveCallCount { get; private set; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task CloseAsync(bool saveChanges, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SheetSummary>> ListSheetsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SheetSummary>>(Array.Empty<SheetSummary>());
        public Task<IReadOnlyList<TableSummary>> ListTablesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TableSummary>>(Array.Empty<TableSummary>());
        public Task<IReadOnlyList<QuerySummary>> ListQueriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QuerySummary>>(Array.Empty<QuerySummary>());
        public Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ConnectionSummary>>(Array.Empty<ConnectionSummary>());
        public Task<IReadOnlyList<NameSummary>> ListNamesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NameSummary>>(Array.Empty<NameSummary>());
        public Task<QueryDefinition> GetQueryAsync(string queryName, CancellationToken cancellationToken = default) => Task.FromResult(new QueryDefinition(queryName, string.Empty));
        public Task<NameSummary> GetNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) => Task.FromResult(new NameSummary(name, sheetName is null ? "Workbook" : "Worksheet", sheetName, string.Empty, null));
        public Task CreateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateNameAsync(string name, string refersTo, string? sheetName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteNameAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetQueryFormulaAsync(string queryName, string formula, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
        public Task<RefreshResult> RefreshQueryAsync(string queryName, RefreshOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new RefreshResult(true, queryName, "query", TimeSpan.Zero));
        public Task<ProbeResult> RunQueryProbeAsync(QueryProbeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));
        public Task<CleanupResult> CleanupTempQueriesAsync(string prefixOrPattern, CancellationToken cancellationToken = default) => Task.FromResult(new CleanupResult(0, Array.Empty<string>()));
        public Task<TableDetailResult> GetTableAsync(string tableName, CancellationToken cancellationToken = default) => Task.FromResult(new TableDetailResult(tableName, "Sheet1", "$A$1", Array.Empty<string>(), 0, 0, true, false, false, null));
        public Task<TableReadResult> ReadTableAsync(string tableName, CancellationToken cancellationToken = default) => Task.FromResult(new TableReadResult(tableName, "Sheet1", "$A$1", Array.Empty<string>(), Array.Empty<IReadOnlyList<object?>>(), false));
        public Task CreateTableAsync(TableCreateRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResizeTableAsync(TableResizeRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceTableRowsAsync(TableRowsWriteRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetTableOptionsAsync(TableOptionsUpdateRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RangeData> ReadRangeAsync(string address, string? sheetName = null, CancellationToken cancellationToken = default) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", address, new object?[,] { { null } }));
        public Task<RangeData> ReadNamedRangeAsync(string name, string? sheetName = null, CancellationToken cancellationToken = default) => Task.FromResult(new RangeData(sheetName ?? "Sheet1", "$A$1", new object?[,] { { null } }));
        public Task WriteRangeAsync(string address, object?[,] values, string? sheetName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
