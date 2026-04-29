using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveRefreshTests
{
    [LiveExcelFact]
    public async Task RefreshQueryAsync_SucceedsForKnownLoadedQuery()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var refresh = await context.WorkbookService.RefreshQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemovedLoaded",
            new RefreshOptions(Silent: true));

        Assert.True(refresh.Succeeded);
        Assert.Equal("tbleWithErrorRemovedLoaded", refresh.Target);
        Assert.Contains(refresh.Mode, new[] { "query-table", "connection" });
        Assert.True(refresh.Duration >= TimeSpan.Zero);

        var queries = await context.WorkbookService.ListQueriesAsync(context.WorkbookPath);
        Assert.Contains(queries, query => query.Name == "tbleWithErrorRemovedLoaded");
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
