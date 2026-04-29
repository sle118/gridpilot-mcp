using ExcelMcp.Bridge.Services;
using ExcelMcp.Core.Results;
using ExcelMcp.UnitTests.Fakes;

namespace ExcelMcp.UnitTests.Services;

public sealed class WorkbookServiceTests
{
    [Fact]
    public async Task InventoryMethods_ReturnDataFromWorkbookHandle()
    {
        var fakeWorkbook = new FakeWorkbookHandle
        {
            Sheets = [new SheetSummary("Sheet1", "Worksheet", true)],
            Tables = [new TableSummary("Sheet1", "SalesTable", "$A$1:$D$12", true, "SalesQuery")],
            Queries = [new QuerySummary("SalesQuery", true, false, "let Source = 1 in Source")],
            Connections = [new ConnectionSummary("Query - SalesQuery", "2", true)]
        };

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var sheets = await sut.ListSheetsAsync("C:/temp/book.xlsx");
        var tables = await sut.ListTablesAsync("C:/temp/book.xlsx");
        var queries = await sut.ListQueriesAsync("C:/temp/book.xlsx");
        var connections = await sut.ListConnectionsAsync("C:/temp/book.xlsx");

        Assert.Equal(fakeWorkbook.Sheets, sheets);
        Assert.Equal(fakeWorkbook.Tables, tables);
        Assert.Equal(fakeWorkbook.Queries, queries);
        Assert.Equal(fakeWorkbook.Connections, connections);
    }

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

    [Fact]
    public async Task CleanupTempQueriesAsync_SavesWorkbookWhenQueriesWereDeleted()
    {
        var fakeWorkbook = new FakeWorkbookHandle();
        fakeWorkbook.OnCleanupAsync = _ => Task.FromResult(new CleanupResult(
            DeletedCount: 2,
            DeletedNames: ["tmp_probe_one", "tmp_probe_two"],
            FailedNames: Array.Empty<string>(),
            Errors: Array.Empty<OperationError>()));

        var session = new FakeExcelSession { Workbook = fakeWorkbook };
        var sut = new WorkbookService(session);

        var result = await sut.CleanupTempQueriesAsync("C:/temp/book.xlsx", "tmp_probe_");

        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(1, fakeWorkbook.SaveCallCount);
    }
}
