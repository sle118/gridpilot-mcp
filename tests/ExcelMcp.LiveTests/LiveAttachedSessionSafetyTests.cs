using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveAttachedSessionSafetyTests
{
    [AttachedLiveExcelFact]
    public async Task AttachedSession_ReadOnlyInventory_IsAllowed()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var inventory = await context.WorkbookService.ListInventoryAsync(context.WorkbookPath);

        Assert.NotEmpty(inventory.Queries);
        Assert.Contains(inventory.Queries, query => query.Name == "tbleWithErrorRemoved");
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_RangeRead_IsAllowedWithoutApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var range = await context.WorkbookService.ReadRangeAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            "A1:B2");

        Assert.Equal("tbleWithErrorRemovedLoaded", range.SheetName);
        Assert.Equal(2, range.Values.Count);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_FormatRead_IsAllowedWithoutApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var format = await context.WorkbookService.ReadRangeFormatAsync(
            context.WorkbookPath,
            "Sheet1",
            "A1");

        Assert.True(format.Succeeded);
        Assert.Equal("Sheet1", format.SheetName);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_MutatingRefresh_IsBlockedWithoutApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            new RefreshOptions(Silent: true));

        Assert.False(refresh.Succeeded);
        Assert.NotNull(refresh.Error);
        Assert.Equal("shared_session_approval_required", refresh.Error!.Code);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Probe_IsBlockedWithoutApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var probe = await context.WorkbookService.TryRunQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            "tmp_probe");

        Assert.False(probe.Succeeded);
        Assert.NotNull(probe.Error);
        Assert.Equal("shared_session_approval_required", probe.Error!.Code);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Cleanup_IsBlockedWithoutApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var cleanup = await context.WorkbookService.CleanupTempQueriesAsync(
            context.WorkbookPath,
            "tmp_probe");

        Assert.Equal(0, cleanup.DeletedCount);
        Assert.NotNull(cleanup.Errors);
        Assert.Single(cleanup.Errors);
        Assert.Equal("shared_session_approval_required", cleanup.Errors[0].Code);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Refresh_WorksAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        await context.GrantApprovalAsync();

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            new RefreshOptions(Silent: true));

        Assert.True(refresh.Succeeded);
        Assert.Null(refresh.Error);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Probe_WorksAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        await context.GrantApprovalAsync();

        var probe = await context.WorkbookService.TryRunQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemoved",
            "tmp_probe");

        Assert.True(probe.Succeeded);
        Assert.Null(probe.Error);
        Assert.NotNull(probe.Preview);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Cleanup_WorksAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        await context.GrantApprovalAsync();

        var cleanup = await context.WorkbookService.CleanupTempQueriesAsync(
            context.WorkbookPath,
            "tmp_probe_nonexistent");

        Assert.NotNull(cleanup);
        Assert.Empty(cleanup.Errors ?? []);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Revoke_ReblocksMutation()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        await context.GrantApprovalAsync();
        await context.RevokeApprovalAsync();

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            new RefreshOptions(Silent: true));

        Assert.False(refresh.Succeeded);
        Assert.Equal("shared_session_approval_required", refresh.Error?.Code);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_ExpiredApproval_ReblocksMutation()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        await context.GrantApprovalAsync(TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            new RefreshOptions(Silent: true));

        Assert.False(refresh.Succeeded);
        Assert.Equal("shared_session_approval_expired", refresh.Error?.Code);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_QueryFormulaUpdate_FailsBeforeApproval_AndSucceedsAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var blocked = await context.WorkbookService.SetQueryFormulaAsync(
            context.WorkbookPath,
            "tbleWithErrorRemoved",
            "let Source = #table({\"Value\"}, {{4321}}) in Source");

        Assert.False(blocked.Succeeded);
        Assert.Equal("shared_session_approval_required", blocked.Error?.Code);

        await context.GrantApprovalAsync();

        var updated = await context.WorkbookService.SetQueryFormulaAsync(
            context.WorkbookPath,
            "tbleWithErrorRemoved",
            "let Source = #table({\"Value\"}, {{4321}}) in Source");

        Assert.True(updated.Succeeded);
        var query = await context.GetQueryAsync("tbleWithErrorRemoved");
        Assert.Contains("4321", query.Formula, StringComparison.Ordinal);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_RangeWrite_FailsBeforeApproval_AndSucceedsAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        var request = new RangeWriteRequest(
        [
            new RangeWriteTarget("tbleWithErrorRemovedLoaded", "Z3:AA3", new object?[,] { { "alpha", "beta" } }),
            new RangeWriteTarget("tbleWithErrorRemovedLoaded", "Z4:AA4", new object?[,] { { 30d, 40d } })
        ]);

        var blocked = await context.WorkbookService.WriteRangesAsync(context.WorkbookPath, request);
        Assert.False(blocked.Succeeded);
        Assert.Equal("shared_session_approval_required", blocked.Error?.Code);

        await context.GrantApprovalAsync();

        var written = await context.WorkbookService.WriteRangesAsync(context.WorkbookPath, request);
        Assert.True(written.Succeeded);

        var firstRow = await context.ReadRangeAsync("tbleWithErrorRemovedLoaded", "Z3:AA3");
        var secondRow = await context.ReadRangeAsync("tbleWithErrorRemovedLoaded", "Z4:AA4");
        Assert.Equal("alpha", firstRow.Values[1, 1]?.ToString());
        Assert.Equal("beta", firstRow.Values[1, 2]?.ToString());
        Assert.Equal(30d, Convert.ToDouble(secondRow.Values[1, 1]));
        Assert.Equal(40d, Convert.ToDouble(secondRow.Values[1, 2]));
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_RangeFormulaWriteAndClear_FailBeforeApproval_AndSucceedAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        const string sheetName = "tbleWithErrorRemovedLoaded";

        var formulaRequest = new RangeFormulaWriteRequest(
        [
            new RangeFormulaWriteTarget(sheetName, "Y5:Z5", new string?[,] { { "=1+1", "=2+2" } })
        ]);

        var blockedWrite = await context.WorkbookService.WriteRangeFormulasAsync(context.WorkbookPath, formulaRequest);
        Assert.False(blockedWrite.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedWrite.Error?.Code);

        var clearRequest = new RangeClearRequest([new RangeClearTarget(sheetName, "Y5:Z5")]);
        var blockedClear = await context.WorkbookService.ClearRangesAsync(context.WorkbookPath, clearRequest);
        Assert.False(blockedClear.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedClear.Error?.Code);

        await context.GrantApprovalAsync();

        var written = await context.WorkbookService.WriteRangeFormulasAsync(context.WorkbookPath, formulaRequest);
        Assert.True(written.Succeeded);

        var formulas = await context.WorkbookService.ReadRangeFormulasAsync(context.WorkbookPath, sheetName, "Y5:Z5");
        Assert.Equal("=1+1", formulas.Formulas[0][0]);
        Assert.Equal("=2+2", formulas.Formulas[0][1]);

        var cleared = await context.WorkbookService.ClearRangesAsync(context.WorkbookPath, clearRequest);
        Assert.True(cleared.Succeeded);

        var clearedFormulas = await context.WorkbookService.ReadRangeFormulasAsync(context.WorkbookPath, sheetName, "Y5:Z5");
        Assert.Null(clearedFormulas.Formulas[0][0]);
        Assert.Null(clearedFormulas.Formulas[0][1]);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_NameCreate_FailsBeforeApproval_AndSucceedsAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var blocked = await context.WorkbookService.CreateNameAsync(
            context.WorkbookPath,
            "GridPilotAttachedName",
            "=tbleWithErrorRemovedLoaded!$Z$5:$AA$5");

        Assert.False(blocked.Succeeded);
        Assert.Equal("shared_session_approval_required", blocked.Error?.Code);

        await context.GrantApprovalAsync();

        var created = await context.WorkbookService.CreateNameAsync(
            context.WorkbookPath,
            "GridPilotAttachedName",
            "=tbleWithErrorRemovedLoaded!$Z$5:$AA$5");

        Assert.True(created.Succeeded);

        var name = await context.WorkbookService.GetNameAsync(context.WorkbookPath, "GridPilotAttachedName");
        Assert.Equal("GridPilotAttachedName", name.Name);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_TableReadAndGet_AreAllowedWithoutApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var table = (await context.WorkbookService.ListTablesAsync(context.WorkbookPath))
            .First(entry => string.Equals(entry.SheetName, "tbleWithErrorRemovedLoaded", StringComparison.OrdinalIgnoreCase));

        var read = await context.WorkbookService.ReadTableAsync(context.WorkbookPath, table.TableName);
        var detail = await context.WorkbookService.GetTableAsync(context.WorkbookPath, table.TableName);

        Assert.Equal(table.TableName, read.TableName);
        Assert.Equal(table.TableName, detail.TableName);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_TableMutations_FailBeforeApproval_AndSucceedAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        const string sheetName = "tbleWithErrorRemovedLoaded";
        const string tableName = "GridPilotAttachedTable";

        await context.GrantApprovalAsync();
        await context.WorkbookService.WriteRangesAsync(
            context.WorkbookPath,
            new RangeWriteRequest(
            [
                new RangeWriteTarget(sheetName, "AB1:AC3", new object?[,]
                {
                    { "Name", "Value" },
                    { "One", 1d },
                    { "Two", 2d }
                })
            ]));
        await context.RevokeApprovalAsync();

        var blockedCreate = await context.WorkbookService.CreateTableAsync(
            context.WorkbookPath,
            new TableCreateRequest(tableName, sheetName, "AB1:AC3"));
        Assert.False(blockedCreate.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedCreate.Error?.Code);

        await context.GrantApprovalAsync();

        var created = await context.WorkbookService.CreateTableAsync(
            context.WorkbookPath,
            new TableCreateRequest(tableName, sheetName, "AB1:AC3"));
        Assert.True(created.Succeeded);

        await context.RevokeApprovalAsync();
        var blockedAppend = await context.WorkbookService.AppendTableRowsAsync(
            context.WorkbookPath,
            new TableRowsWriteRequest(tableName, new object?[,] { { "Three", 3d } }));
        Assert.False(blockedAppend.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedAppend.Error?.Code);

        await context.GrantApprovalAsync();

        Assert.True((await context.WorkbookService.AppendTableRowsAsync(
            context.WorkbookPath,
            new TableRowsWriteRequest(tableName, new object?[,] { { "Three", 3d } }))).Succeeded);

        Assert.True((await context.WorkbookService.ResizeTableAsync(
            context.WorkbookPath,
            new TableResizeRequest(tableName, sheetName, "AB1:AC5"))).Succeeded);

        Assert.True((await context.WorkbookService.ReplaceTableRowsAsync(
            context.WorkbookPath,
            new TableRowsWriteRequest(tableName, new object?[,] { { "Four", 4d }, { "Five", 5d }, { "Six", 6d } }))).Succeeded);

        Assert.True((await context.WorkbookService.SetTableOptionsAsync(
            context.WorkbookPath,
            new TableOptionsUpdateRequest(tableName, ShowTotals: true))).Succeeded);

        var read = await context.WorkbookService.ReadTableAsync(context.WorkbookPath, tableName);
        Assert.Equal(3, read.Rows.Count);
        Assert.True(read.HasTotalsRow);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_WorksheetAndTableDelete_FailBeforeApproval_AndSucceedAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        const string sheetName = "GridPilotAttachedDeleteSheet";
        const string tableName = "GridPilotAttachedDeleteTable";

        await context.GrantApprovalAsync();

        Assert.True((await context.WorkbookService.CreateWorksheetAsync(context.WorkbookPath, sheetName)).Succeeded);

        Assert.True((await context.WorkbookService.WriteRangesAsync(
            context.WorkbookPath,
            new RangeWriteRequest(
            [
                new RangeWriteTarget(sheetName, "A1:B3", new object?[,]
                {
                    { "Name", "Value" },
                    { "One", 1d },
                    { "Two", 2d }
                })
            ]))).Succeeded);

        Assert.True((await context.WorkbookService.CreateTableAsync(
            context.WorkbookPath,
            new TableCreateRequest(tableName, sheetName, "A1:B3"))).Succeeded);

        await context.RevokeApprovalAsync();

        var blockedDeleteTable = await context.WorkbookService.DeleteTableAsync(context.WorkbookPath, tableName);
        Assert.False(blockedDeleteTable.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedDeleteTable.Error?.Code);

        var blockedDeleteWorksheet = await context.WorkbookService.DeleteWorksheetAsync(context.WorkbookPath, sheetName);
        Assert.False(blockedDeleteWorksheet.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedDeleteWorksheet.Error?.Code);

        await context.GrantApprovalAsync();

        var deletedTable = await context.WorkbookService.DeleteTableAsync(context.WorkbookPath, tableName);
        Assert.True(deletedTable.Succeeded);
        Assert.Equal(sheetName, deletedTable.SheetName);

        var deletedWorksheet = await context.WorkbookService.DeleteWorksheetAsync(context.WorkbookPath, sheetName);
        Assert.True(deletedWorksheet.Succeeded);

        var inventory = await context.WorkbookService.ListInventoryAsync(context.WorkbookPath);
        Assert.DoesNotContain(inventory.Sheets, sheet => string.Equals(sheet.Name, sheetName, StringComparison.Ordinal));
        Assert.DoesNotContain(inventory.Tables, table => string.Equals(table.TableName, tableName, StringComparison.OrdinalIgnoreCase));
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_FormattingAndWorksheetLayoutMutations_FailBeforeApproval_AndSucceedAfterApproval()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var sheetName = $"GPLAttach{suffix}";

        await context.GrantApprovalAsync();
        Assert.True((await context.WorkbookService.CreateWorksheetAsync(context.WorkbookPath, sheetName)).Succeeded);
        await context.RevokeApprovalAsync();

        var blockedFormat = await context.WorkbookService.WriteRangeFormatsAsync(
            context.WorkbookPath,
            new RangeFormatWriteRequest(
            [
                new RangeFormatWriteTarget("Sheet1", "F20", new RangeFormatPatch(Bold: true, FillColor: "#CCDD11"))
            ]));
        Assert.False(blockedFormat.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedFormat.Error?.Code);

        var blockedAutofit = await context.WorkbookService.AutofitRangesAsync(
            context.WorkbookPath,
            new RangeAutofitRequest([new RangeAutofitTarget("Sheet1", "F20", "columns")]));
        Assert.False(blockedAutofit.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedAutofit.Error?.Code);

        var blockedVisibility = await context.WorkbookService.SetWorksheetVisibilityAsync(
            context.WorkbookPath,
            new WorksheetVisibilityRequest(sheetName, "hidden"));
        Assert.False(blockedVisibility.Succeeded);
        Assert.Equal("shared_session_approval_required", blockedVisibility.Error?.Code);

        await context.GrantApprovalAsync();

        var written = await context.WorkbookService.WriteRangeFormatsAsync(
            context.WorkbookPath,
            new RangeFormatWriteRequest(
            [
                new RangeFormatWriteTarget("Sheet1", "F20", new RangeFormatPatch(Bold: true, FillColor: "#CCDD11", ColumnWidth: 7d))
            ]));
        Assert.True(written.Succeeded);

        var autofit = await context.WorkbookService.AutofitRangesAsync(
            context.WorkbookPath,
            new RangeAutofitRequest([new RangeAutofitTarget("Sheet1", "F20", "columns")]));
        Assert.True(autofit.Succeeded);

        var hidden = await context.WorkbookService.SetWorksheetVisibilityAsync(
            context.WorkbookPath,
            new WorksheetVisibilityRequest(sheetName, "veryHidden"));
        Assert.True(hidden.Succeeded);

        var inventory = await context.WorkbookService.ListInventoryAsync(context.WorkbookPath);
        var entry = Assert.Single(inventory.Sheets, sheet => string.Equals(sheet.Name, sheetName, StringComparison.Ordinal));
        Assert.Equal("veryHidden", entry.Visibility);

        var format = await context.WorkbookService.ReadRangeFormatAsync(context.WorkbookPath, "Sheet1", "F20");
        Assert.True(format.Succeeded);
        Assert.True(format.Format.Bold);
    }
}
