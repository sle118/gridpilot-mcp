using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveWorkbookEditTests
{
    [LiveExcelFact]
    public async Task SetQueryFormulaAsync_PersistsUpdatedFormula()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var update = await context.WorkbookService.SetQueryFormulaAsync(
            context.WorkbookPath,
            "tbleWithErrorRemoved",
            "let Source = #table({\"Value\"}, {{1234}}) in Source");

        Assert.True(update.Succeeded);

        await using var workbook = await context.OpenWorkbookAsync();
        var query = await workbook.GetQueryAsync("tbleWithErrorRemoved");
        Assert.Contains("1234", query.Formula, StringComparison.Ordinal);
    }

    [LiveExcelFact]
    public async Task ReadRangeAsync_ReturnsKnownCellValues()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var range = await context.WorkbookService.ReadRangeAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            "A1:B2");

        Assert.Equal("tbleWithErrorRemovedLoaded", range.SheetName);
        Assert.Equal(2, range.Values.Count);
        Assert.True(range.Values[0].Count >= 2);
    }

    [LiveExcelFact]
    public async Task WriteRangesAsync_PersistsMultipleWrites()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var result = await context.WorkbookService.WriteRangesAsync(
            context.WorkbookPath,
            new RangeWriteRequest(
            [
                new RangeWriteTarget("tbleWithErrorRemovedLoaded", "Z1:AA1", new object?[,] { { "left", "right" } }),
                new RangeWriteTarget("tbleWithErrorRemovedLoaded", "Z2:AA2", new object?[,] { { 10d, 20d } })
            ]));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.WriteCount);

        await using var workbook = await context.OpenWorkbookAsync();
        var topRow = await workbook.ReadRangeAsync("Z1:AA1", "tbleWithErrorRemovedLoaded");
        var secondRow = await workbook.ReadRangeAsync("Z2:AA2", "tbleWithErrorRemovedLoaded");
        Assert.Equal("left", topRow.Values[1, 1]?.ToString());
        Assert.Equal("right", topRow.Values[1, 2]?.ToString());
        Assert.Equal(10d, Convert.ToDouble(secondRow.Values[1, 1]));
        Assert.Equal(20d, Convert.ToDouble(secondRow.Values[1, 2]));
    }
}
