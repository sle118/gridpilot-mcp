using ExcelMcp.Bridge.Services;
using ExcelMcp.Core.Results;
using ExcelMcp.UnitTests.Fakes;

namespace ExcelMcp.UnitTests.Services;

public sealed class WorkbookServiceTests
{
    [Fact]
    public async Task TryRunQueryAsync_UsesGeneratedTempNameWithPrefix()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        QueryProbeRequest? captured = null;
        fakeWorkbook.OnRunProbeAsync = request =>
        {
            captured = request;
            return Task.FromResult(new ProbeResult(true, request.TargetQueryName, request.TempQueryName));
        };

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.TryRunQueryAsync("C:/temp/book.xlsx", "SalesQuery", "tmp_probe");

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal("SalesQuery", captured!.TargetQueryName);
        Assert.StartsWith("tmp_probe_SalesQuery_", captured.TempQueryName, StringComparison.Ordinal);
    }
}
