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
    public async Task AttachedSession_MutatingRefresh_IsBlockedWhenWorkbookIsAlreadyOpen()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            new RefreshOptions(Silent: true));

        Assert.False(refresh.Succeeded);
        Assert.NotNull(refresh.Error);
        Assert.Equal("shared_session_workbook_owned_in_attached_session", refresh.Error!.Code);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Probe_IsBlockedWhenWorkbookIsAlreadyOpen()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var probe = await context.WorkbookService.TryRunQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            "tmp_probe");

        Assert.False(probe.Succeeded);
        Assert.NotNull(probe.Error);
        Assert.Equal("shared_session_workbook_owned_in_attached_session", probe.Error!.Code);
    }

    [AttachedLiveExcelFact]
    public async Task AttachedSession_Cleanup_IsBlockedWhenWorkbookIsAlreadyOpen()
    {
        await using var context = await AttachedLiveExcelTestContext.CreateAsync();

        var cleanup = await context.WorkbookService.CleanupTempQueriesAsync(
            context.WorkbookPath,
            "tmp_probe");

        Assert.Equal(0, cleanup.DeletedCount);
        Assert.NotNull(cleanup.Errors);
        Assert.Single(cleanup.Errors);
        Assert.Equal("shared_session_workbook_owned_in_attached_session", cleanup.Errors[0].Code);
    }
}
