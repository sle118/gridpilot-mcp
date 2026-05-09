using ExcelMcp.ComAdapter.Interop;
using ExcelMcp.Core.Results;
using System.Runtime.Versioning;

namespace ExcelMcp.UnitTests.Interop;

[SupportedOSPlatform("windows")]
public sealed class ComWorkbookHandleTests
{
    [Fact]
    public async Task InventoryMethods_MapSheetsTablesQueriesAndConnections()
    {
        var workbook = new FakeWorkbookComObject
        {
            Sheets =
            [
                new FakeSheetComObject
                {
                    Name = "Sheet1",
                    Type = "Worksheet",
                    Visible = -1,
                    ListObjects =
                    [
                        new FakeListObjectComObject
                        {
                            Name = "SalesTable",
                            Range = new FakeRangeComObject("$A$1:$D$12"),
                            QueryTable = new FakeQueryTableComObject
                            {
                                WorkbookConnection = new FakeConnectionComObject
                                {
                                    Name = "Query - SalesQuery",
                                    Type = 2,
                                    RefreshWithRefreshAll = true
                                }
                            }
                        }
                    ]
                },
                new FakeSheetComObject
                {
                    Name = "HiddenSheet",
                    Type = "Worksheet",
                    Visible = 0,
                    ListObjects = []
                }
            ],
            QueryItems =
            [
                new FakeQueryComObject("SalesQuery", "let Source = 1 in Source"),
                new FakeQueryComObject("ModelOnly", "let Source = 2 in Source")
            ],
            Connections =
            [
                new FakeConnectionComObject { Name = "Query - SalesQuery", Type = 2, RefreshWithRefreshAll = true },
                new FakeConnectionComObject { Name = "Query - ModelOnly", Type = 7, RefreshWithRefreshAll = false }
            ]
        };

        await using var sut = new ComWorkbookHandle(workbook);

        var sheets = await sut.ListSheetsAsync();
        var tables = await sut.ListTablesAsync();
        var queries = await sut.ListQueriesAsync();
        var connections = await sut.ListConnectionsAsync();
        var query = await sut.GetQueryAsync("SalesQuery");

        Assert.Collection(
            sheets,
            sheet =>
            {
                Assert.Equal("Sheet1", sheet.Name);
                Assert.True(sheet.Visible);
                Assert.Equal("visible", sheet.Visibility);
                Assert.Equal(1, sheet.Index);
            },
            sheet =>
            {
                Assert.Equal("HiddenSheet", sheet.Name);
                Assert.False(sheet.Visible);
                Assert.Equal("hidden", sheet.Visibility);
                Assert.Equal(2, sheet.Index);
            });

        var table = Assert.Single(tables);
        Assert.Equal("Sheet1", table.SheetName);
        Assert.Equal("SalesTable", table.TableName);
        Assert.Equal("$A$1:$D$12", table.Address);
        Assert.True(table.IsQueryBacked);
        Assert.Equal("SalesQuery", table.QueryName);

        Assert.Collection(
            queries.OrderBy(q => q.Name),
            modelOnly =>
            {
                Assert.Equal("ModelOnly", modelOnly.Name);
                Assert.False(modelOnly.LoadToWorksheet);
                Assert.True(modelOnly.LoadToDataModel);
                Assert.Equal("let Source = 2 in Source", modelOnly.Formula);
            },
            sales =>
            {
                Assert.Equal("SalesQuery", sales.Name);
                Assert.True(sales.LoadToWorksheet);
                Assert.False(sales.LoadToDataModel);
                Assert.Equal("let Source = 1 in Source", sales.Formula);
            });

        Assert.Equal(2, connections.Count);
        Assert.Equal("SalesQuery", query.Name);
        Assert.Equal("let Source = 1 in Source", query.Formula);
    }

    [Fact]
    public async Task CleanupTempQueriesAsync_DeletesMatchingQueriesOnly()
    {
        var keep = new FakeQueryComObject("CustomerQuery", "let Source = 1 in Source");
        var delete1 = new FakeQueryComObject("tmp_probe_sales", "let Source = 2 in Source");
        var delete2 = new FakeQueryComObject("tmp_probe_margin", "let Source = 3 in Source");

        await using var sut = new ComWorkbookHandle(new FakeWorkbookComObject
        {
            QueryItems = [keep, delete1, delete2]
        });

        var result = await sut.CleanupTempQueriesAsync("tmp_probe_");

        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(["tmp_probe_sales", "tmp_probe_margin"], result.DeletedNames);
        Assert.Empty(result.FailedNames ?? []);
        Assert.Empty(result.Errors ?? []);
        Assert.False(keep.Deleted);
        Assert.True(delete1.Deleted);
        Assert.True(delete2.Deleted);
    }

    [Fact]
    public async Task CleanupTempQueriesAsync_ReturnsNoOpWhenNothingMatches()
    {
        await using var sut = new ComWorkbookHandle(new FakeWorkbookComObject
        {
            QueryItems = [new FakeQueryComObject("SalesQuery", "let Source = 1 in Source")]
        });

        var first = await sut.CleanupTempQueriesAsync("tmp_probe_");
        var second = await sut.CleanupTempQueriesAsync("tmp_probe_");

        Assert.Equal(0, first.DeletedCount);
        Assert.Empty(first.DeletedNames);
        Assert.Empty(first.FailedNames ?? []);
        Assert.Empty(first.Errors ?? []);
        Assert.Equal(0, second.DeletedCount);
        Assert.Empty(second.DeletedNames);
        Assert.Empty(second.FailedNames ?? []);
        Assert.Empty(second.Errors ?? []);
    }

    [Fact]
    public async Task CleanupTempQueriesAsync_IsIdempotentAfterDeletion()
    {
        await using var sut = new ComWorkbookHandle(new FakeWorkbookComObject
        {
            QueryItems = [new FakeQueryComObject("tmp_probe_sales", "let Source = 1 in Source")]
        });

        var first = await sut.CleanupTempQueriesAsync("tmp_probe_");
        var second = await sut.CleanupTempQueriesAsync("tmp_probe_");

        Assert.Equal(1, first.DeletedCount);
        Assert.Equal(["tmp_probe_sales"], first.DeletedNames);
        Assert.Equal(0, second.DeletedCount);
        Assert.Empty(second.DeletedNames);
        Assert.Empty(second.FailedNames ?? []);
        Assert.Empty(second.Errors ?? []);
    }

    [Fact]
    public async Task CleanupTempQueriesAsync_ReportsPartialFailures()
    {
        var deletable = new FakeQueryComObject("tmp_probe_ok", "let Source = 1 in Source");
        var failing = new FakeQueryComObject("tmp_probe_fail", "let Source = 2 in Source")
        {
            DeleteException = new InvalidOperationException("locked")
        };

        await using var sut = new ComWorkbookHandle(new FakeWorkbookComObject
        {
            QueryItems = [deletable, failing]
        });

        var result = await sut.CleanupTempQueriesAsync("tmp_probe_*");

        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(["tmp_probe_ok"], result.DeletedNames);
        Assert.Equal(["tmp_probe_fail"], result.FailedNames);
        var error = Assert.Single(result.Errors ?? []);
        Assert.Equal("query_cleanup_failed", error.Code);
        Assert.Contains("tmp_probe_fail", error.Message, StringComparison.Ordinal);
        Assert.Equal("locked", error.Detail);
        Assert.True(deletable.Deleted);
        Assert.False(failing.Deleted);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotCloseBorrowedWorkbookHandle()
    {
        var workbook = new FakeWorkbookComObject();
        await using (var sut = new ComWorkbookHandle(workbook, closeOnDispose: false))
        {
            _ = sut.Name;
        }

        Assert.Equal(0, workbook.CloseCallCount);
    }

    private sealed class FakeWorkbookComObject
    {
        public string Name { get; init; } = "fake.xlsx";
        public string FullName { get; init; } = @"C:\temp\fake.xlsx";
        public List<FakeSheetComObject> Sheets { get; init; } = [];
        public List<FakeQueryComObject> QueryItems { get; init; } = [];
        public IEnumerable<FakeQueryComObject> Queries => QueryItems.Where(query => !query.Deleted);
        public List<FakeConnectionComObject> Connections { get; init; } = [];
        public int CloseCallCount { get; private set; }

        public void Close(bool saveChanges)
        {
            CloseCallCount++;
        }
    }

    private sealed class FakeSheetComObject
    {
        public string Name { get; init; } = string.Empty;
        public string Type { get; init; } = "Worksheet";
        public object Visible { get; init; } = -1;
        public List<FakeListObjectComObject> ListObjects { get; init; } = [];
    }

    private sealed class FakeListObjectComObject
    {
        public string Name { get; init; } = string.Empty;
        public FakeRangeComObject Range { get; init; } = new(string.Empty);
        public FakeQueryTableComObject? QueryTable { get; init; }
    }

    private sealed class FakeRangeComObject(string address)
    {
        public string Address() => address;
        public string Address(bool rowAbsolute, bool columnAbsolute) => address;
    }

    private sealed class FakeQueryTableComObject
    {
        public FakeConnectionComObject? WorkbookConnection { get; init; }
    }

    private sealed class FakeConnectionComObject
    {
        public string Name { get; init; } = string.Empty;
        public object Type { get; init; } = "Unknown";
        public bool RefreshWithRefreshAll { get; init; }
    }

    private sealed class FakeQueryComObject(string name, string formula)
    {
        public string Name { get; } = name;
        public string Formula { get; } = formula;
        public string Description { get; init; } = string.Empty;
        public bool Deleted { get; private set; }
        public Exception? DeleteException { get; init; }

        public void Delete()
        {
            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            Deleted = true;
        }
    }
}
