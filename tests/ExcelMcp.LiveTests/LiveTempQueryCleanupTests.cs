using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveTempQueryCleanupTests
{
    [LiveExcelFact]
    public async Task CleanupTempQueriesAsync_DeletesOnlyTemporaryQueries_AndIsIdempotent()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();
        const string prefix = "tmp_probe_live_";
        var tempQuery1 = $"{prefix}one";
        var tempQuery2 = $"{prefix}two";

        await using (var workbook = await context.OpenWorkbookAsync())
        {
            await workbook.SetQueryFormulaAsync(tempQuery1, "let Source = #table({\"Value\"}, {{1}}) in Source");
            await workbook.SetQueryFormulaAsync(tempQuery2, "let Source = #table({\"Value\"}, {{2}}) in Source");
            await workbook.SaveAsync();
        }

        var beforeCleanup = await context.WorkbookService.ListQueriesAsync(context.WorkbookPath);
        Assert.Contains(beforeCleanup, query => query.Name == tempQuery1);
        Assert.Contains(beforeCleanup, query => query.Name == tempQuery2);
        Assert.Contains(beforeCleanup, query => query.Name == "tbleWithErrorOnChangedType");

        var firstCleanup = await context.WorkbookService.CleanupTempQueriesAsync(context.WorkbookPath, prefix);
        Assert.Equal(2, firstCleanup.DeletedCount);
        Assert.Contains(tempQuery1, firstCleanup.DeletedNames);
        Assert.Contains(tempQuery2, firstCleanup.DeletedNames);
        Assert.Empty(firstCleanup.FailedNames ?? []);
        Assert.Empty(firstCleanup.Errors ?? []);

        var afterCleanup = await context.WorkbookService.ListQueriesAsync(context.WorkbookPath);
        Assert.DoesNotContain(afterCleanup, query => query.Name == tempQuery1);
        Assert.DoesNotContain(afterCleanup, query => query.Name == tempQuery2);
        Assert.Contains(afterCleanup, query => query.Name == "tbleWithErrorOnChangedType");

        var secondCleanup = await context.WorkbookService.CleanupTempQueriesAsync(context.WorkbookPath, prefix);
        Assert.Equal(0, secondCleanup.DeletedCount);
        Assert.Empty(secondCleanup.DeletedNames);
        Assert.Empty(secondCleanup.FailedNames ?? []);
        Assert.Empty(secondCleanup.Errors ?? []);
    }
}
