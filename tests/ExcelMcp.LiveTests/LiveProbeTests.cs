using ExcelMcp.LiveTests.Infrastructure;
using System.Runtime.Versioning;

namespace ExcelMcp.LiveTests;

[SupportedOSPlatform("windows")]
public sealed class LiveProbeTests
{
    [LiveExcelFact]
    public async Task TryRunQueryAsync_ReturnsPreviewRowsAndCleansUpTempArtifacts()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();
        var baselineConnections = await context.WorkbookService.ListConnectionsAsync(context.WorkbookPath);

        var probe = await context.WorkbookService.TryRunQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorRemoved",
            "tmp_probe_live");

        Assert.True(probe.Succeeded);
        Assert.NotNull(probe.Preview);
        Assert.StartsWith("tmp_probe_live_tbleWithErrorRemoved_", probe.TempQuery, StringComparison.Ordinal);
        Assert.True(probe.Preview!.Values.Count >= 2);

        var queries = await context.WorkbookService.ListQueriesAsync(context.WorkbookPath);
        Assert.DoesNotContain(queries, query => query.Name == probe.TempQuery);

        var connections = await context.WorkbookService.ListConnectionsAsync(context.WorkbookPath);
        Assert.Equal(
            baselineConnections.Select(connection => connection.Name).OrderBy(name => name),
            connections.Select(connection => connection.Name).OrderBy(name => name));
    }

    [LiveExcelFact]
    public async Task TryRunQueryAsync_CanProbeKnownErrorQuery()
    {
        await using var context = await LiveExcelTestContext.CreateAsync();

        var probe = await context.WorkbookService.TryRunQueryAsync(
            context.WorkbookPath,
            "tbleWithErrorOnChangedType",
            "tmp_probe_live");

        Assert.True(probe.Succeeded);
        Assert.NotNull(probe.Preview);
        Assert.True(probe.Preview!.Values.Count >= 2);
        Assert.Contains("Some text", probe.Preview.Values[1][1]?.ToString(), StringComparison.Ordinal);
    }
}
