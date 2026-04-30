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
}
