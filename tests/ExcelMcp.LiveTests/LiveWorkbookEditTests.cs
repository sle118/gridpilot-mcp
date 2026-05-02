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
    public async Task RangeFormulasAndClear_PersistAndPreserveFormatting()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();
        const string sheetName = "tbleWithErrorRemovedLoaded";

        var seed = await context.WorkbookService.WriteRangesAsync(
            context.WorkbookPath,
            new RangeWriteRequest(
            [
                new RangeWriteTarget(sheetName, "Y1:Z2", new object?[,]
                {
                    { 1d, 2d },
                    { 3d, 4d }
                })
            ]));
        Assert.True(seed.Succeeded);

        await using var workbook = await context.OpenWorkbookAsync();
        var beforeClear = await workbook.ReadRangeAsync("Y1:Z2", sheetName);
        var beforeAddress = beforeClear.Address;

        var write = await context.WorkbookService.WriteRangeFormulasAsync(
            context.WorkbookPath,
            new RangeFormulaWriteRequest(
            [
                new RangeFormulaWriteTarget(sheetName, "Y1:Z2", new string?[,]
                {
                    { "=1+1", "=2+2" },
                    { "=3+3", "=4+4" }
                })
            ]));
        Assert.True(write.Succeeded);

        var formulas = await context.WorkbookService.ReadRangeFormulasAsync(context.WorkbookPath, sheetName, "Y1:Z2");
        Assert.Equal("=1+1", formulas.Formulas[0][0]);
        Assert.Equal("=4+4", formulas.Formulas[1][1]);

        var cleared = await context.WorkbookService.ClearRangesAsync(
            context.WorkbookPath,
            new RangeClearRequest([new RangeClearTarget(sheetName, "Y1:Z2")]));
        Assert.True(cleared.Succeeded);

        var afterValues = await workbook.ReadRangeAsync("Y1:Z2", sheetName);
        var afterFormulas = await context.WorkbookService.ReadRangeFormulasAsync(context.WorkbookPath, sheetName, "Y1:Z2");
        Assert.Equal(beforeAddress, afterValues.Address);
        Assert.Null(afterValues.Values[1, 1]);
        Assert.Null(afterValues.Values[2, 2]);
        Assert.Null(afterFormulas.Formulas[0][0]);
        Assert.Null(afterFormulas.Formulas[1][1]);
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

    [LiveExcelFact]
    public async Task PersistenceAndWorksheetLifecycle_SaveSaveAsAndDeleteTable_Persist()
    {
        string? savedWorkbookPath = null;

        try
        {
            await using var context = await LiveExcelTestContext.CreateAsync();
            const string createdSheetName = "GridPilotTempSheet";
            const string renamedSheetName = "GridPilotRenamedSheet";
            const string tableName = "GridPilotDeleteMe";

            var saved = await context.WorkbookService.SaveWorkbookAsync(context.WorkbookPath);
            Assert.True(saved.Succeeded);

            var created = await context.WorkbookService.CreateWorksheetAsync(context.WorkbookPath, createdSheetName);
            Assert.True(created.Succeeded);

            var renamed = await context.WorkbookService.RenameWorksheetAsync(
                context.WorkbookPath,
                createdSheetName,
                renamedSheetName);
            Assert.True(renamed.Succeeded);

            var write = await context.WorkbookService.WriteRangesAsync(
                context.WorkbookPath,
                new RangeWriteRequest(
                [
                    new RangeWriteTarget(renamedSheetName, "A1:B3", new object?[,]
                    {
                        { "Name", "Value" },
                        { "One", 1d },
                        { "Two", 2d }
                    })
                ]));
            Assert.True(write.Succeeded);

            var createdTable = await context.WorkbookService.CreateTableAsync(
                context.WorkbookPath,
                new TableCreateRequest(tableName, renamedSheetName, "A1:B3"));
            Assert.True(createdTable.Succeeded);

            var deletedTable = await context.WorkbookService.DeleteTableAsync(context.WorkbookPath, tableName);
            Assert.True(deletedTable.Succeeded);
            Assert.Equal(renamedSheetName, deletedTable.SheetName);

            savedWorkbookPath = Path.Combine(
                Path.GetDirectoryName(context.WorkbookPath)!,
                $"saved-copy-{Guid.NewGuid():N}.xlsx");

            var savedAs = await context.WorkbookService.SaveWorkbookAsAsync(context.WorkbookPath, savedWorkbookPath);
            Assert.True(savedAs.Succeeded);
            Assert.Equal(savedWorkbookPath, savedAs.WorkbookPath);

            var savedInventory = await context.WorkbookService.ListInventoryAsync(savedWorkbookPath);
            Assert.Contains(savedInventory.Sheets, sheet => string.Equals(sheet.Name, renamedSheetName, StringComparison.Ordinal));
            Assert.DoesNotContain(savedInventory.Tables, table => string.Equals(table.TableName, tableName, StringComparison.OrdinalIgnoreCase));

            var deletedWorksheet = await context.WorkbookService.DeleteWorksheetAsync(savedWorkbookPath, renamedSheetName);
            Assert.True(deletedWorksheet.Succeeded);

            var rereadInventory = await context.WorkbookService.ListInventoryAsync(savedWorkbookPath);
            Assert.DoesNotContain(rereadInventory.Sheets, sheet => string.Equals(sheet.Name, renamedSheetName, StringComparison.Ordinal));

            var resaved = await context.WorkbookService.SaveWorkbookAsync(savedWorkbookPath);
            Assert.True(resaved.Succeeded);
        }
        finally
        {
            DeleteTempWorkbookWithRetry(savedWorkbookPath);
        }
    }

    private static void DeleteTempWorkbookWithRetry(string? workbookPath)
    {
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(workbookPath))
                {
                    File.Delete(workbookPath);
                }

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(250);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(250);
            }
        }
    }
}
