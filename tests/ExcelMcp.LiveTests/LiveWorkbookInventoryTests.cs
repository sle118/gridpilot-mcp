using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveWorkbookInventoryTests
{
    private static readonly string[] ExpectedQueries =
    [
        "tbleDirectRefreshLoaded",
        "tbleWithErrorOnChangedType",
        "tbleWithErrorRemoved",
        "tbleWithErrorRemovedLoaded",
        "tbleWithErrorOnChangedTypeLoaded"
    ];

    [LiveExcelFact]
    public async Task Inventory_ReturnsExpectedQueriesSheetsTablesAndConnections()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var queries = await context.WorkbookService.ListQueriesAsync(context.WorkbookPath);
        var sheets = await context.WorkbookService.ListSheetsAsync(context.WorkbookPath);
        var tables = await context.WorkbookService.ListTablesAsync(context.WorkbookPath);
        var connections = await context.WorkbookService.ListConnectionsAsync(context.WorkbookPath);

        Assert.Equal(ExpectedQueries.OrderBy(x => x), queries.Select(q => q.Name).OrderBy(x => x));

        var loadedQueries = queries.Where(q => q.LoadToWorksheet).Select(q => q.Name).OrderBy(x => x).ToArray();
        Assert.Equal(
            new[] { "tbleDirectRefreshLoaded", "tbleWithErrorOnChangedTypeLoaded", "tbleWithErrorRemovedLoaded" },
            loadedQueries);

        Assert.Contains(queries, query => query.Name == "tbleWithErrorOnChangedTypeLoaded" && !string.IsNullOrWhiteSpace(query.Formula));
        Assert.Contains(queries, query => query.Name == "tbleDirectRefreshLoaded" && !string.IsNullOrWhiteSpace(query.Formula));
        Assert.Contains(queries, query => query.Name == "tbleWithErrorRemoved" && !string.IsNullOrWhiteSpace(query.Formula));

        Assert.Contains(sheets, sheet => sheet.Name == "tbleDirectRefreshLoaded");
        Assert.Contains(sheets, sheet => sheet.Name == "tbleWithErrorOnChangedTypeLoade");
        Assert.Contains(sheets, sheet => sheet.Name == "tbleWithErrorRemovedLoaded");

        Assert.Contains(tables, table => table.QueryName == "tbleDirectRefreshLoaded");
        Assert.Contains(tables, table => table.QueryName == "tbleWithErrorOnChangedTypeLoaded");
        Assert.Contains(tables, table => table.QueryName == "tbleWithErrorRemovedLoaded");

        Assert.True(connections.Count >= 5);
        Assert.Contains(connections, connection => connection.Name.Contains("tbleWithErrorOnChangedType", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(connections, connection => connection.Name.Contains("tbleWithErrorRemoved", StringComparison.OrdinalIgnoreCase));
    }

    [LiveExcelFact]
    public async Task GetQueryAsync_ReturnsFormulaForKnownQueries()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var errorQuery = await context.WorkbookService.GetQueryAsync(context.WorkbookPath, "tbleWithErrorOnChangedTypeLoaded");
        var filteredQuery = await context.WorkbookService.GetQueryAsync(context.WorkbookPath, "tbleWithErrorRemoved");

        Assert.False(string.IsNullOrWhiteSpace(errorQuery.Formula));
        Assert.False(string.IsNullOrWhiteSpace(filteredQuery.Formula));
        Assert.Contains("let", errorQuery.Formula, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("let", filteredQuery.Formula, StringComparison.OrdinalIgnoreCase);
    }

    [LiveExcelFact]
    public async Task ReadTableAsync_ReturnsHeadersAndRowsForKnownLoadedTable()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var tableSummary = (await context.WorkbookService.ListTablesAsync(context.WorkbookPath))
            .First(table => string.Equals(table.QueryName, "tbleWithErrorRemovedLoaded", StringComparison.OrdinalIgnoreCase));

        var table = await context.WorkbookService.ReadTableAsync(context.WorkbookPath, tableSummary.TableName);

        Assert.Equal(tableSummary.TableName, table.TableName);
        Assert.Equal(tableSummary.SheetName, table.SheetName);
        Assert.NotEmpty(table.Headers);
        Assert.NotEmpty(table.Rows);
    }
}
