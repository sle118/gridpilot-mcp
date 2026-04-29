using ExcelMcp.Bridge.Contracts;
using ExcelMcp.Bridge.Services;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.IntegrationTests.Fakes;
using ExcelMcp.ToolHost.Mcp;
using System.Text.Json;

namespace ExcelMcp.IntegrationTests;

public sealed class McpToolServerTests
{
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
                ToolNames.QueryCleanupTemp
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
            OpenWorkbooks = [new WorkbookSummary("book.xlsx", @"C:\temp\book.xlsx", true)]
        };
        var server = new McpToolServer(new WorkbookService(fakeSession));
        var args = JsonSerializer.SerializeToElement(new { workbookPath = @"C:\temp\book.xlsx", queryName = "SalesQuery" });

        var result = await server.CallToolAsync(ToolNames.QueryRefresh, args);

        Assert.True(result.IsError);
        Assert.False(result.StructuredContent.GetProperty("succeeded").GetBoolean());
        Assert.Equal("shared_session_unsafe", result.StructuredContent.GetProperty("error").GetProperty("code").GetString());
    }

    private static McpToolServer CreateServer(FakeWorkbookHandle? workbook = null)
    {
        var fakeSession = new FakeExcelSession { Workbook = workbook ?? new FakeWorkbookHandle() };
        return new McpToolServer(new WorkbookService(fakeSession));
    }
}
