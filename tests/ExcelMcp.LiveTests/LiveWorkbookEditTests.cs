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

    [LiveExcelFact]
    public async Task NameLifecycle_PersistsCreateUpdateAndDelete()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var created = await context.WorkbookService.CreateNameAsync(
            context.WorkbookPath,
            "GridPilotTempName",
            "=tbleWithErrorRemovedLoaded!$Z$1:$AA$1");

        Assert.True(created.Succeeded);

        var name = await context.WorkbookService.GetNameAsync(context.WorkbookPath, "GridPilotTempName");
        Assert.Equal("Workbook", name.Scope);
        Assert.Equal("=tbleWithErrorRemovedLoaded!$Z$1:$AA$1", name.RefersTo);

        var updated = await context.WorkbookService.UpdateNameAsync(
            context.WorkbookPath,
            "GridPilotTempName",
            "=tbleWithErrorRemovedLoaded!$Z$2:$AA$2");

        Assert.True(updated.Succeeded);

        var reread = await context.WorkbookService.GetNameAsync(context.WorkbookPath, "GridPilotTempName");
        Assert.Equal("=tbleWithErrorRemovedLoaded!$Z$2:$AA$2", reread.RefersTo);

        var deleted = await context.WorkbookService.DeleteNameAsync(
            context.WorkbookPath,
            "GridPilotTempName");

        Assert.True(deleted.Succeeded);

        var names = await context.WorkbookService.ListNamesAsync(context.WorkbookPath);
        Assert.DoesNotContain(names, entry => string.Equals(entry.Name, "GridPilotTempName", StringComparison.OrdinalIgnoreCase));
    }

    [LiveExcelFact]
    public async Task TableLifecycle_CreateAppendResizeReplaceAndOptions_Persist()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();
        const string sheetName = "tbleWithErrorRemovedLoaded";
        const string tableName = "GridPilotTempTable";

        await context.WorkbookService.WriteRangesAsync(
            context.WorkbookPath,
            new RangeWriteRequest(
            [
                new RangeWriteTarget(sheetName, "Z1:AA3", new object?[,]
                {
                    { "Name", "Value" },
                    { "One", 1d },
                    { "Two", 2d }
                })
            ]));

        var created = await context.WorkbookService.CreateTableAsync(
            context.WorkbookPath,
            new TableCreateRequest(tableName, sheetName, "Z1:AA3"));

        Assert.True(created.Succeeded);

        var detail = await context.WorkbookService.GetTableAsync(context.WorkbookPath, tableName);
        Assert.Equal(2, detail.ColumnCount);
        Assert.Equal(2, detail.RowCount);

        var appended = await context.WorkbookService.AppendTableRowsAsync(
            context.WorkbookPath,
            new TableRowsWriteRequest(tableName, new object?[,] { { "Three", 3d } }));

        Assert.True(appended.Succeeded);

        var resized = await context.WorkbookService.ResizeTableAsync(
            context.WorkbookPath,
            new TableResizeRequest(tableName, sheetName, "Z1:AA5"));

        Assert.True(resized.Succeeded);

        var replaced = await context.WorkbookService.ReplaceTableRowsAsync(
            context.WorkbookPath,
            new TableRowsWriteRequest(tableName, new object?[,] { { "Four", 4d }, { "Five", 5d }, { "Six", 6d } }));

        Assert.True(replaced.Succeeded);

        var options = await context.WorkbookService.SetTableOptionsAsync(
            context.WorkbookPath,
            new TableOptionsUpdateRequest(tableName, ShowTotals: true));

        Assert.True(options.Succeeded);

        var reread = await context.WorkbookService.ReadTableAsync(context.WorkbookPath, tableName);
        Assert.Equal(3, reread.Rows.Count);
        Assert.Equal("Four", reread.Rows[0][0]?.ToString());
        Assert.Equal(6d, Convert.ToDouble(reread.Rows[2][1]));
        Assert.True(reread.HasTotalsRow);
    }
}
