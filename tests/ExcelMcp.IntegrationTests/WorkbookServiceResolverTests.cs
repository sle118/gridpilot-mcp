using ExcelMcp.ToolHost;

namespace ExcelMcp.IntegrationTests;

public sealed class WorkbookServiceResolverTests
{
    [Theory]
    [InlineData(
        @"https:\d.docs.live.net\171321e0a36cf836\Documents\Book_mcp_test.xlsx",
        "https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx")]
    [InlineData(
        @"https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx",
        "https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx")]
    [InlineData(
        @"file:///C:/temp/Book1.xlsx",
        @"C:\temp\Book1.xlsx")]
    [InlineData(
        @"file://localhost/C:/temp/Book1.xlsx",
        @"C:\temp\Book1.xlsx")]
    [InlineData(
        @"C:\temp\Book1.xlsx",
        @"C:\temp\Book1.xlsx")]
    public void NormalizePathOrNull_PreservesWorkbookIdentityShape(string rawPath, string expectedNormalizedPath)
    {
        Assert.Equal(expectedNormalizedPath, WorkbookServiceResolver.NormalizePathOrNull(rawPath));
    }
}
