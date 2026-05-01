using ExcelMcp.Bridge.Contracts;
using ExcelMcp.Bridge.Services;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;
using ExcelMcp.IntegrationTests.Fakes;
using ExcelMcp.Core;
using ExcelMcp.ToolHost;
using ExcelMcp.ToolHost.Mcp;
using System.Text.Json;

namespace ExcelMcp.IntegrationTests;

public sealed class McpToolServerTests
{
    [Fact]
    public void Initialize_ReturnsServerInfoAndProtocolVersion()
    {
        var server = CreateServer();

        var result = server.Initialize("2024-11-05");

        Assert.Equal("2024-11-05", result.ProtocolVersion);
        Assert.Equal("GridPilot MCP", result.ServerInfo.GetType().GetProperty("name")?.GetValue(result.ServerInfo));
    }

    [Fact]
    public void ListTools_ReturnsOnlyTheNarrowSupportedSurface()
    {
        var server = CreateServer();

        var tools = server.ListTools();

        Assert.Equal(
            new[]
            {
                ToolNames.SessionListOpenWorkbooks,
                ToolNames.SessionConnectWorkbook,
                ToolNames.SessionListConnections,
                ToolNames.SessionGetConnection,
                ToolNames.SessionDisconnectWorkbook,
                ToolNames.WorkbookListInventory,
                ToolNames.WorkbookListNames,
                ToolNames.QueryGet,
                ToolNames.NameGet,
                ToolNames.NameRead,
                ToolNames.NameCreate,
                ToolNames.NameUpdate,
                ToolNames.NameDelete,
                ToolNames.QueryRefresh,
                ToolNames.QueryRunProbe,
                ToolNames.QueryCleanupTemp,
                ToolNames.QuerySetFormula,
                ToolNames.TableGet,
                ToolNames.TableRead,
                ToolNames.TableCreate,
                ToolNames.TableResize,
                ToolNames.TableAppendRows,
                ToolNames.TableReplaceRows,
                ToolNames.TableSetOptions,
                ToolNames.RangeRead,
                ToolNames.RangeWrite,
                ToolNames.AttachedSessionGrantMutation,
                ToolNames.AttachedSessionRevokeMutation
            },
            tools.Select(tool => tool.Name).ToArray());
    }

    [Fact]
    public async Task CallToolAsync_ListOpenWorkbooks_ReturnsStructuredContent()
    {
        var server = new McpToolServer(new ConnectionAwareResolver());

        var result = await server.CallToolAsync(
            ToolNames.SessionListOpenWorkbooks,
            JsonSerializer.SerializeToElement(new { }));

        Assert.False(result.IsError);
        Assert.Equal("Book1.xlsx", result.StructuredContent[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task CallToolAsync_ConnectWorkbook_ReturnsApprovalState()
    {
        var server = new McpToolServer(new ConnectionAwareResolver());

        var result = await server.CallToolAsync(
            ToolNames.SessionConnectWorkbook,
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\connected.xlsx" }));

        Assert.False(result.IsError);
        Assert.Equal("missing", result.StructuredContent.GetProperty("approvalState").GetString());
        Assert.Equal(JsonValueKind.Null, result.StructuredContent.GetProperty("approvalExpiresAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.StructuredContent.GetProperty("approvalLastUsedAtUtc").ValueKind);
    }

    [Fact]
    public async Task CallToolAsync_ListConnections_ReturnsApprovalState()
    {
        var server = new McpToolServer(new ConnectionAwareResolver());

        var result = await server.CallToolAsync(
            ToolNames.SessionListConnections,
            JsonSerializer.SerializeToElement(new { }));

        Assert.False(result.IsError);
        Assert.Equal("missing", result.StructuredContent[0].GetProperty("approvalState").GetString());
    }

    [Fact]
    public async Task CallToolAsync_GetConnection_ReturnsApprovalState()
    {
        var server = new McpToolServer(new ConnectionAwareResolver());

        var result = await server.CallToolAsync(
            ToolNames.SessionGetConnection,
            JsonSerializer.SerializeToElement(new { connectionId = "conn-1" }));

        Assert.False(result.IsError);
        Assert.Equal("missing", result.StructuredContent.GetProperty("approvalState").GetString());
    }

    [Fact]
    public async Task CallToolAsync_UsesConnectionIdWhenWorkbookPathIsOmitted()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Queries = [new QuerySummary("SalesQuery", true, false, "let Source = 1 in Source")]
        };
        var resolver = new ConnectionAwareResolver(new WorkbookService(new FakeExcelSession { Workbook = fakeWorkbook }));
        var server = new McpToolServer(resolver);

        var result = await server.CallToolAsync(
            ToolNames.WorkbookListInventory,
            JsonSerializer.SerializeToElement(new { connectionId = "conn-1" }));

        Assert.False(result.IsError);
        Assert.Equal(@"C:\temp\connected.xlsx", resolver.LastResolvedPath);
        Assert.Single(result.StructuredContent.GetProperty("queries").EnumerateArray());
    }

    [Fact]
    public async Task CallToolAsync_ReturnsStructuredInventoryContent()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Sheets = [new SheetSummary("Sheet1", "Worksheet", true)],
            Tables = [new TableSummary("Sheet1", "SalesTable", "$A$1:$D$2", true, "SalesQuery")],
            Queries = [new QuerySummary("SalesQuery", true, false, "let Source = 1 in Source")],
            Connections = [new ConnectionSummary("Query - SalesQuery", "1", true)]
        };

        var server = CreateServer(fakeWorkbook);
        var args = JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" });

        var result = await server.CallToolAsync(ToolNames.WorkbookListInventory, args);

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.TryGetProperty("sheets", out var sheets));
        Assert.Equal(1, sheets.GetArrayLength());
        Assert.True(result.StructuredContent.TryGetProperty("queries", out var queries));
        Assert.Equal("SalesQuery", queries[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task CallToolAsync_ListNames_ReturnsStructuredNameContent()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Names = [new NameSummary("SalesRange", "Workbook", null, "=Sheet1!$A$1:$B$2", "$A$1:$B$2")]
        };

        var server = CreateServer(fakeWorkbook);
        var args = JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" });

        var result = await server.CallToolAsync(ToolNames.WorkbookListNames, args);

        Assert.False(result.IsError);
        Assert.Equal("SalesRange", result.StructuredContent[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task CallToolAsync_MapsStructuredSafetyFailureForRefresh()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var fakeSession = new FakeExcelSession
        {
            Workbook = fakeWorkbook,
            Diagnostics = new SessionDiagnostics(ExcelSessionMode.AttachToRunning, true, true, ExcelCalculationState.Done, SessionAttachTargetMode.WorkbookOwner)
        };
        var approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        var server = new McpToolServer(new WorkbookService(fakeSession, new WorkbookOperationSafety(fakeSession, approvalRegistry)));
        var args = JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx", queryName = "SalesQuery" });

        var result = await server.CallToolAsync(ToolNames.QueryRefresh, args);

        Assert.True(result.IsError);
        Assert.False(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Equal("shared_session_approval_required", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallToolAsync_ReturnsStructuredErrorForMissingArguments()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            ToolNames.QueryGet,
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" }));

        Assert.True(result.IsError);
        Assert.Equal("invalid_arguments", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallToolAsync_ReturnsStructuredErrorForUnknownTool()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            "unknown_tool",
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" }));

        Assert.True(result.IsError);
        Assert.Equal("invalid_tool", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallToolAsync_ReturnsStructuredErrorForAttachTargetFailure()
    {
        var server = new McpToolServer(new ThrowingWorkbookServiceResolver(
            new ExcelSessionTargetException(
                "attach_target_no_matching_instance",
                "No running Excel instance currently has workbook open.",
                "Open the workbook first.")));

        var result = await server.CallToolAsync(
            ToolNames.WorkbookListInventory,
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" }));

        Assert.True(result.IsError);
        Assert.Equal("attach_target_no_matching_instance", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Open the workbook first.", result.StructuredContent.GetProperty("error").GetProperty("detail").GetString());
    }

    [Fact]
    public async Task CallToolAsync_ReturnsStructuredErrorWhenToolExecutionTimesOut()
    {
        var server = new McpToolServer(new HangingWorkbookServiceResolver(), toolExecutionTimeout: TimeSpan.FromMilliseconds(50));

        var result = await server.CallToolAsync(
            ToolNames.SessionListOpenWorkbooks,
            JsonSerializer.SerializeToElement(new { }));

        Assert.True(result.IsError);
        Assert.Equal("tool_timeout", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallToolAsync_GrantApproval_ReturnsLeaseMetadata()
    {
        var server = new McpToolServer(new ApprovalCapableWorkbookServiceResolver());

        var result = await server.CallToolAsync(
            ToolNames.AttachedSessionGrantMutation,
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx", ttlMinutes = 15 }));

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Equal(@"C:\temp\book.xlsx", result.StructuredContent.GetProperty("workbookPath").GetString());
        Assert.Equal(15, (int)(DateTimeOffset.Parse(result.StructuredContent.GetProperty("expiresAtUtc").GetString()!) -
                               DateTimeOffset.Parse(result.StructuredContent.GetProperty("grantedAtUtc").GetString()!)).TotalMinutes);
    }

    [Fact]
    public async Task CallToolAsync_GrantApproval_PreservesUrlStyleWorkbookIdentity()
    {
        var server = new McpToolServer(new ApprovalCapableWorkbookServiceResolver());

        var result = await server.CallToolAsync(
            ToolNames.AttachedSessionGrantMutation,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = "https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx",
                ttlMinutes = 15
            }));

        Assert.False(result.IsError);
        Assert.Equal(
            "https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx",
            result.StructuredContent.GetProperty("workbookPath").GetString());
    }

    [Fact]
    public async Task CallToolAsync_ListConnections_ShowsActiveApprovalAfterGrant()
    {
        var resolver = new ApprovalAwareConnectionResolver();
        var server = new McpToolServer(resolver);

        var grant = await server.CallToolAsync(
            ToolNames.AttachedSessionGrantMutation,
            JsonSerializer.SerializeToElement(new { connectionId = "conn-1" }));

        Assert.False(grant.IsError);

        var result = await server.CallToolAsync(
            ToolNames.SessionListConnections,
            JsonSerializer.SerializeToElement(new { }));

        Assert.False(result.IsError);
        Assert.Equal("active", result.StructuredContent[0].GetProperty("approvalState").GetString());
        Assert.Equal(JsonValueKind.String, result.StructuredContent[0].GetProperty("approvalExpiresAtUtc").ValueKind);
    }

    [Fact]
    public async Task CallToolAsync_GetConnection_ShowsMissingApprovalAfterRevoke()
    {
        var resolver = new ApprovalAwareConnectionResolver();
        var server = new McpToolServer(resolver);

        var grant = await server.CallToolAsync(
            ToolNames.AttachedSessionGrantMutation,
            JsonSerializer.SerializeToElement(new { connectionId = "conn-1" }));
        Assert.False(grant.IsError);

        var revoke = await server.CallToolAsync(
            ToolNames.AttachedSessionRevokeMutation,
            JsonSerializer.SerializeToElement(new { connectionId = "conn-1" }));
        Assert.False(revoke.IsError);

        var result = await server.CallToolAsync(
            ToolNames.SessionGetConnection,
            JsonSerializer.SerializeToElement(new { connectionId = "conn-1" }));

        Assert.False(result.IsError);
        Assert.Equal("missing", result.StructuredContent.GetProperty("approvalState").GetString());
        Assert.Equal(JsonValueKind.Null, result.StructuredContent.GetProperty("approvalExpiresAtUtc").ValueKind);
    }

    [Fact]
    public async Task CallToolAsync_QuerySetFormula_ReturnsStructuredSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var server = CreateServer(fakeWorkbook);

        var result = await server.CallToolAsync(
            ToolNames.QuerySetFormula,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                queryName = "SalesQuery",
                formula = "let Source = 1 in Source"
            }));

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Equal("SalesQuery", result.StructuredContent.GetProperty("queryName").GetString());
        Assert.Single(fakeWorkbook.SetQueryFormulaCalls);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task CallToolAsync_NameGet_ReturnsStructuredSuccess()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            ToolNames.NameGet,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                name = "SalesRange"
            }));

        Assert.False(result.IsError);
        Assert.Equal("SalesRange", result.StructuredContent.GetProperty("name").GetString());
        Assert.Equal("Workbook", result.StructuredContent.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task CallToolAsync_NameRead_ReturnsStructuredValues()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            ToolNames.NameRead,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                name = "SalesRange"
            }));

        Assert.False(result.IsError);
        Assert.Equal("Sheet1", result.StructuredContent.GetProperty("sheetName").GetString());
        Assert.Equal("value", result.StructuredContent.GetProperty("values")[0][0].GetString());
    }

    [Fact]
    public async Task CallToolAsync_NameCreate_ReturnsStructuredSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var server = CreateServer(fakeWorkbook);

        var result = await server.CallToolAsync(
            ToolNames.NameCreate,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                name = "SalesRange",
                refersTo = "=Sheet1!$A$1:$B$2"
            }));

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Equal("create", result.StructuredContent.GetProperty("action").GetString());
        Assert.Single(fakeWorkbook.CreatedNames);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task CallToolAsync_NameUpdate_ReturnsStructuredSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var server = CreateServer(fakeWorkbook);

        var result = await server.CallToolAsync(
            ToolNames.NameUpdate,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                name = "LocalRange",
                refersTo = "=Sheet1!$C$1",
                sheetName = "Sheet1"
            }));

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Equal("Worksheet", result.StructuredContent.GetProperty("scope").GetString());
        Assert.Single(fakeWorkbook.UpdatedNames);
    }

    [Fact]
    public async Task CallToolAsync_NameDelete_ReturnsStructuredSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var server = CreateServer(fakeWorkbook);

        var result = await server.CallToolAsync(
            ToolNames.NameDelete,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                name = "SalesRange"
            }));

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Single(fakeWorkbook.DeletedNames);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task CallToolAsync_TableRead_ReturnsStructuredHeadersAndRows()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            ToolNames.TableRead,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                tableName = "SalesTable"
            }));

        Assert.False(result.IsError);
        Assert.Equal("SalesTable", result.StructuredContent.GetProperty("tableName").GetString());
        Assert.Equal("Column1", result.StructuredContent.GetProperty("headers")[0].GetString());
        Assert.Equal(1d, result.StructuredContent.GetProperty("rows")[0][0].GetDouble());
    }

    [Fact]
    public async Task CallToolAsync_TableGet_ReturnsStructuredMetadata()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            ToolNames.TableGet,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                tableName = "SalesTable"
            }));

        Assert.False(result.IsError);
        Assert.Equal("SalesTable", result.StructuredContent.GetProperty("tableName").GetString());
        Assert.Equal(2, result.StructuredContent.GetProperty("columnCount").GetInt32());
    }

    [Fact]
    public async Task CallToolAsync_TableCreate_ReturnsStructuredSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var server = CreateServer(fakeWorkbook);

        var result = await server.CallToolAsync(
            ToolNames.TableCreate,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                tableName = "GridPilotTable",
                sheetName = "Sheet1",
                address = "Z1:AA2"
            }));

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Single(fakeWorkbook.CreatedTables);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task CallToolAsync_TableAppendRows_ReturnsStructuredSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var server = CreateServer(fakeWorkbook);

        var result = await server.CallToolAsync(
            ToolNames.TableAppendRows,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                tableName = "SalesTable",
                values = new object?[][] { new object?[] { "A", "B" } }
            }));

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Single(fakeWorkbook.AppendedTableRows);
    }

    [Fact]
    public async Task CallToolAsync_TableSetOptions_ReturnsStructuredErrorWhenNoOptionsWereProvided()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            ToolNames.TableSetOptions,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                tableName = "SalesTable"
            }));

        Assert.True(result.IsError);
        Assert.Equal("invalid_arguments", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallToolAsync_RangeRead_ReturnsStructuredValues()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        var server = CreateServer(fakeWorkbook);

        var result = await server.CallToolAsync(
            ToolNames.RangeRead,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                sheetName = "Sheet1",
                address = "A1"
            }));

        Assert.False(result.IsError);
        Assert.Equal("Sheet1", result.StructuredContent.GetProperty("sheetName").GetString());
        Assert.Equal("A1", result.StructuredContent.GetProperty("address").GetString());
        Assert.Equal("value", result.StructuredContent.GetProperty("values")[0][0].GetString());
    }

    [Fact]
    public async Task CallToolAsync_RangeWrite_ReturnsStructuredSuccess()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.ReadRangeCalls.Clear();
        var server = CreateServer(fakeWorkbook);
        fakeWorkbook.ReadRangeCalls.Clear();

        var result = await server.CallToolAsync(
            ToolNames.RangeWrite,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                writes = new object[]
                {
                    new { sheetName = "Sheet1", address = "A1:B1", values = new object?[][] { new object?[] { "A", "B" } } },
                    new { sheetName = "Sheet1", address = "A2:B2", values = new object?[][] { new object?[] { "C", "D" } } }
                }
            }));

        if (result.IsError)
        {
            throw new Xunit.Sdk.XunitException(result.StructuredContent.ToString());
        }

        Assert.True(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Equal(2, result.StructuredContent.GetProperty("writeCount").GetInt32());
        Assert.Equal(2, fakeWorkbook.WriteRangeCalls.Count);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }

    [Fact]
    public async Task CallToolAsync_RangeWrite_ReturnsStructuredErrorForMalformedValues()
    {
        var server = CreateServer();

        var result = await server.CallToolAsync(
            ToolNames.RangeWrite,
            JsonSerializer.SerializeToElement(new
            {
                workbookPath = @"C:\temp\book.xlsx",
                writes = new object[]
                {
                    new { sheetName = "Sheet1", address = "A1:B2", values = new object?[][] { new object?[] { "A" }, new object?[] { "B", "C" } } }
                }
            }));

        Assert.True(result.IsError);
        Assert.Equal("invalid_arguments", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CallToolAsync_RevokeApproval_IsIdempotent()
    {
        var resolver = new ApprovalCapableWorkbookServiceResolver();
        var server = new McpToolServer(resolver);

        var first = await server.CallToolAsync(
            ToolNames.AttachedSessionRevokeMutation,
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" }));
        var second = await server.CallToolAsync(
            ToolNames.AttachedSessionRevokeMutation,
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" }));

        Assert.False(first.IsError);
        Assert.True(first.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.True(first.StructuredContent.GetProperty("leaseExisted").GetBoolean());
        Assert.False(second.IsError);
        Assert.False(second.StructuredContent.GetProperty("leaseExisted").GetBoolean());
    }

    [Fact]
    public async Task CallToolAsync_ReturnsStructuredErrorWhenApprovalNotApplicable()
    {
        var server = new McpToolServer(new ThrowingApprovalResolver(new AttachedMutationApprovalModeException(
            "attached_session_approval_not_applicable",
            "Attached-session mutation approval is only available in attach mode.",
            "Restart the host in attach mode.")));

        var result = await server.CallToolAsync(
            ToolNames.AttachedSessionGrantMutation,
            JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx" }));

        Assert.True(result.IsError);
        Assert.Equal("attached_session_approval_not_applicable", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    private static McpToolServer CreateServer(FakeWorkbookHandle? workbook = null)
    {
        var fakeSession = new FakeExcelSession { Workbook = workbook ?? new FakeWorkbookHandle() };
        return new McpToolServer(new WorkbookService(fakeSession));
    }

    private sealed class ThrowingWorkbookServiceResolver : IWorkbookServiceResolver
    {
        private readonly Exception _exception;

        public ThrowingWorkbookServiceResolver(Exception exception)
        {
            _exception = exception;
        }

        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default) =>
            Task.FromException<T>(_exception);

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<WorkbookSummary>>(_exception);

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionResult>(_exception);

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<WorkbookConnectionInfo>>(_exception);

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionInfo>(_exception);

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookDisconnectResult>(_exception);

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalGrantResult>(_exception);

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalRevokeResult>(_exception);
    }

    private sealed class ThrowingApprovalResolver : IWorkbookServiceResolver
    {
        private readonly Exception _exception;

        public ThrowingApprovalResolver(Exception exception)
        {
            _exception = exception;
        }

        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default) =>
            Task.FromException<T>(_exception);

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<WorkbookSummary>>(_exception);

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionResult>(_exception);

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<WorkbookConnectionInfo>>(_exception);

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionInfo>(_exception);

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookDisconnectResult>(_exception);

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalGrantResult>(_exception);

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalRevokeResult>(_exception);
    }

    private sealed class HangingWorkbookServiceResolver : IWorkbookServiceResolver
    {
        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<IReadOnlyList<WorkbookSummary>>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<WorkbookConnectionResult>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<IReadOnlyList<WorkbookConnectionInfo>>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<WorkbookConnectionInfo>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<WorkbookDisconnectResult>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<AttachedMutationApprovalGrantResult>(TaskCreationOptions.RunContinuationsAsynchronously).Task;

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<AttachedMutationApprovalRevokeResult>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
    }

    private sealed class ApprovalCapableWorkbookServiceResolver : IWorkbookServiceResolver
    {
        private readonly InMemoryAttachedMutationApprovalRegistry _registry = new(() => new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero));
        private readonly AttachedMutationApprovalService _service;

        public ApprovalCapableWorkbookServiceResolver()
        {
            _service = new AttachedMutationApprovalService(_registry);
            _registry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(5), out _);
            _registry.Grant("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx", TimeSpan.FromMinutes(5), out _);
        }

        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default) =>
            Task.FromException<T>(new InvalidOperationException("not used"));

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookSummary>>(Array.Empty<WorkbookSummary>());

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionResult>(new InvalidOperationException("not used"));

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookConnectionInfo>>(Array.Empty<WorkbookConnectionInfo>());

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookConnectionInfo>(new InvalidOperationException("not used"));

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromException<WorkbookDisconnectResult>(new InvalidOperationException("not used"));

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            _service.GrantAsync(target.WorkbookPath!, ttl, cancellationToken);

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            _service.RevokeAsync(target.WorkbookPath!, cancellationToken);
    }

    private sealed class ConnectionAwareResolver : IWorkbookServiceResolver
    {
        private readonly WorkbookService _service;

        public ConnectionAwareResolver(WorkbookService? service = null)
        {
            _service = service ?? new WorkbookService(new FakeExcelSession { Workbook = new FakeWorkbookHandle() });
        }

        public string? LastResolvedPath { get; private set; }

        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default)
        {
            var path = target.ConnectionId is not null ? @"C:\temp\connected.xlsx" : target.WorkbookPath!;
            LastResolvedPath = path;
            return action(new ResolvedWorkbookContext(path, target.ConnectionId, _service));
        }

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookSummary>>(
            [
                new WorkbookSummary("Book1.xlsx", @"C:\temp\Book1.xlsx", true)
            ]);

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkbookConnectionResult(
                true,
                "conn-1",
                request.WorkbookName ?? "connected.xlsx",
                request.WorkbookPath ?? @"C:\temp\connected.xlsx",
                "attached",
                "attach",
                "workbook-owner",
                false,
                true,
                "missing",
                null,
                null));

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookConnectionInfo>>(
            [
                new WorkbookConnectionInfo("conn-1", "connected.xlsx", @"C:\temp\connected.xlsx", "attached", "attach", "workbook-owner", true, "missing", null, null)
            ]);

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkbookConnectionInfo(connectionId, "connected.xlsx", @"C:\temp\connected.xlsx", "attached", "attach", "workbook-owner", true, "missing", null, null));

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkbookDisconnectResult(true, connectionId, @"C:\temp\connected.xlsx", true));

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AttachedMutationApprovalGrantResult(true, target.WorkbookPath ?? @"C:\temp\connected.xlsx", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10), false, null));

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AttachedMutationApprovalRevokeResult(true, target.WorkbookPath ?? @"C:\temp\connected.xlsx", true));
    }

    private sealed class ApprovalAwareConnectionResolver : IWorkbookServiceResolver
    {
        private readonly InMemoryAttachedMutationApprovalRegistry _registry = new(() => new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        private readonly AttachedMutationApprovalService _service;
        private const string WorkbookPath = @"C:\temp\connected.xlsx";

        public ApprovalAwareConnectionResolver()
        {
            _service = new AttachedMutationApprovalService(_registry);
        }

        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default) =>
            Task.FromException<T>(new InvalidOperationException("not used"));

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookSummary>>(Array.Empty<WorkbookSummary>());

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(BuildConnectResult(request.WorkbookPath ?? WorkbookPath));

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookConnectionInfo>>([BuildConnectionInfo("conn-1")]);

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(BuildConnectionInfo(connectionId));

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkbookDisconnectResult(true, connectionId, WorkbookPath, true));

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            _service.GrantAsync(target.WorkbookPath ?? WorkbookPath, ttl, cancellationToken);

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            _service.RevokeAsync(target.WorkbookPath ?? WorkbookPath, cancellationToken);

        private WorkbookConnectionInfo BuildConnectionInfo(string connectionId)
        {
            var approval = _registry.Lookup(WorkbookPath);
            return new WorkbookConnectionInfo(
                connectionId,
                "connected.xlsx",
                WorkbookPath,
                "attached",
                "attach",
                "workbook-owner",
                true,
                approval.State switch
                {
                    AttachedMutationApprovalState.Active => "active",
                    AttachedMutationApprovalState.Expired => "expired",
                    _ => "missing"
                },
                approval.Lease?.ExpiresAtUtc,
                approval.Lease?.LastUsedAtUtc);
        }

        private WorkbookConnectionResult BuildConnectResult(string workbookPath)
        {
            var approval = _registry.Lookup(workbookPath);
            return new WorkbookConnectionResult(
                true,
                "conn-1",
                "connected.xlsx",
                workbookPath,
                "attached",
                "attach",
                "workbook-owner",
                false,
                true,
                approval.State switch
                {
                    AttachedMutationApprovalState.Active => "active",
                    AttachedMutationApprovalState.Expired => "expired",
                    _ => "missing"
                },
                approval.Lease?.ExpiresAtUtc,
                approval.Lease?.LastUsedAtUtc);
        }
    }
}
