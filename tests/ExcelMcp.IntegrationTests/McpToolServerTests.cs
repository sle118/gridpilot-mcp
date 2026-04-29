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
                ToolNames.WorkbookListInventory,
                ToolNames.QueryGet,
                ToolNames.QueryRefresh,
                ToolNames.QueryRunProbe,
                ToolNames.QueryCleanupTemp,
                ToolNames.AttachedSessionGrantMutation,
                ToolNames.AttachedSessionRevokeMutation
            },
            tools.Select(tool => tool.Name).ToArray());
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

        public Task<T> ExecuteAsync<T>(string workbookPath, Func<WorkbookService, Task<T>> action, CancellationToken cancellationToken = default) =>
            Task.FromException<T>(_exception);

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(string workbookPath, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalGrantResult>(_exception);

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(string workbookPath, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalRevokeResult>(_exception);
    }

    private sealed class ThrowingApprovalResolver : IWorkbookServiceResolver
    {
        private readonly Exception _exception;

        public ThrowingApprovalResolver(Exception exception)
        {
            _exception = exception;
        }

        public Task<T> ExecuteAsync<T>(string workbookPath, Func<WorkbookService, Task<T>> action, CancellationToken cancellationToken = default) =>
            Task.FromException<T>(_exception);

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(string workbookPath, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalGrantResult>(_exception);

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(string workbookPath, CancellationToken cancellationToken = default) =>
            Task.FromException<AttachedMutationApprovalRevokeResult>(_exception);
    }

    private sealed class ApprovalCapableWorkbookServiceResolver : IWorkbookServiceResolver
    {
        private readonly InMemoryAttachedMutationApprovalRegistry _registry = new(() => new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero));
        private readonly AttachedMutationApprovalService _service;

        public ApprovalCapableWorkbookServiceResolver()
        {
            _service = new AttachedMutationApprovalService(_registry);
            _registry.Grant(@"C:\temp\book.xlsx", TimeSpan.FromMinutes(5), out _);
        }

        public Task<T> ExecuteAsync<T>(string workbookPath, Func<WorkbookService, Task<T>> action, CancellationToken cancellationToken = default) =>
            Task.FromException<T>(new InvalidOperationException("not used"));

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(string workbookPath, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            _service.GrantAsync(workbookPath, ttl, cancellationToken);

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(string workbookPath, CancellationToken cancellationToken = default) =>
            _service.RevokeAsync(workbookPath, cancellationToken);
    }
}
