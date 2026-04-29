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
        Assert.Equal("shared_session_workbook_open", refresh.Error!.Code);
    }
}
