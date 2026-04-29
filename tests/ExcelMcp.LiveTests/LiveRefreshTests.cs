using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveRefreshTests
{
    [LiveExcelFact]
    public async Task RefreshQueryAsync_UpdatesLoadedQueryAfterFormulaChange()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        await using (var workbook = await context.OpenWorkbookAsync())
        {
            await workbook.SetQueryFormulaAsync(
                "tbleWithErrorRemoved",
                "let Source = #table({\"Value\"}, {{999}}) in Source");
            await workbook.SaveAsync();
        }

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            new RefreshOptions(Silent: true, PreferSynchronousTableRefresh: false));

        Assert.True(refresh.Succeeded);
        Assert.Equal("tbleWithErrorRemovedLoaded", refresh.Target);
        Assert.Contains(refresh.Mode, new[] { "query-table", "connection" });
        Assert.True(refresh.Duration >= TimeSpan.Zero);

        await using var reloadedWorkbook = await context.OpenWorkbookAsync();
        var range = await reloadedWorkbook.ReadRangeAsync("A2", "tbleWithErrorRemovedLoaded");
        Assert.Equal(999d, Convert.ToDouble(range.Values[1, 1]));
    }

    [LiveExcelFact]
    public async Task RefreshQueryAsync_ReturnsStructuredFailureForUnknownQuery()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "missing_query",
            new RefreshOptions(Silent: true));

        Assert.False(refresh.Succeeded);
        Assert.NotNull(refresh.Error);
        Assert.Equal("query_not_found", refresh.Error!.Code);
    }
}
