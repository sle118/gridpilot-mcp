using ExcelMcp.ComAdapter.Interop;

namespace ExcelMcp.UnitTests.Interop;

public sealed class RunningWorkbookObjectTableTests
{
    [Theory]
    [InlineData(@"C:\temp\Book1.xlsx")]
    [InlineData(@"C:\temp\Book1.xlsm")]
    [InlineData(@"C:\temp\Book1.xlsb")]
    [InlineData(@"C:\temp\Book1.csv")]
    [InlineData(@"https:\d.docs.live.net\171321e0a36cf836\Documents\Book_mcp_test.xlsx")]
    [InlineData(@"https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx")]
    [InlineData(@"file:///C:/temp/Book1.xlsx")]
    [InlineData(@"file://localhost/C:/temp/Book1.xlsx")]
    public void LooksLikeWorkbookPath_ReturnsTrueForWorkbookFiles(string path)
    {
        Assert.True(RunningWorkbookObjectTable.LooksLikeWorkbookPath(path));
    }

    [Theory]
    [InlineData(@"C:\temp\preview.png")]
    [InlineData(@"C:\temp\notes.txt")]
    [InlineData(@"C:\temp\folder")]
    [InlineData("")]
    [InlineData(null)]
    public void LooksLikeWorkbookPath_ReturnsFalseForNonWorkbookFiles(string? path)
    {
        Assert.False(RunningWorkbookObjectTable.LooksLikeWorkbookPath(path));
    }

    [Theory]
    [InlineData(
        "!https:\\d.docs.live.net\\171321e0a36cf836\\Documents\\Book_mcp_test.xlsx",
        "https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx")]
    [InlineData(
        " https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx ",
        "https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx")]
    [InlineData(
        " file:///C:/temp/Book1.xlsx ",
        @"C:\temp\Book1.xlsx")]
    [InlineData(
        " file://localhost/C:/temp/Book1.xlsx ",
        @"C:\temp\Book1.xlsx")]
    [InlineData(
        @"!C:\temp\Book1.xlsx",
        @"C:\temp\Book1.xlsx")]
    public void TryNormalizeWorkbookCandidatePath_PreservesWorkbookIdentityShape(string displayName, string expectedNormalizedPath)
    {
        Assert.True(RunningWorkbookObjectTable.TryNormalizeWorkbookCandidatePath(displayName, out var normalizedPath));
        Assert.Equal(expectedNormalizedPath, normalizedPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!")]
    [InlineData(@"!C:\temp\preview.png")]
    public void TryNormalizeWorkbookCandidatePath_ReturnsFalseForNonWorkbookDisplayNames(string displayName)
    {
        Assert.False(RunningWorkbookObjectTable.TryNormalizeWorkbookCandidatePath(displayName, out _));
    }

    [Theory]
    [InlineData(
        @"https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx",
        @"https:\d.docs.live.net\171321e0a36cf836\Documents\Book_mcp_test.xlsx",
        @"https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx")]
    [InlineData(
        @"C:\Users\sle11\OneDrive\Documents\Book_mcp_test.xlsx",
        @"https:\d.docs.live.net\171321e0a36cf836\Documents\Book_mcp_test.xlsx",
        @"C:\Users\sle11\OneDrive\Documents\Book_mcp_test.xlsx")]
    [InlineData(
        @"C:\temp\Book1.xlsx",
        @"file:///C:/temp/Book1.xlsx",
        @"C:\temp\Book1.xlsx")]
    [InlineData(
        @"C:\temp\Book1.xlsx",
        @"file://localhost/C:/temp/Book1.xlsx",
        @"C:\temp\Book1.xlsx")]
    [InlineData(
        @"file:///C:/temp/Book1.xlsx",
        @"C:\temp\Book1.xlsx",
        @"C:\temp\Book1.xlsx")]
    public void WorkbookPathMatchesTarget_ReturnsTrueWhenEitherMonikerOrResolvedWorkbookPathMatches(
        string targetPath,
        string candidatePath,
        string resolvedWorkbookPath)
    {
        Assert.True(RunningWorkbookObjectTable.WorkbookPathMatchesTarget(targetPath, candidatePath, resolvedWorkbookPath));
    }

    [Fact]
    public void WorkbookPathMatchesTarget_ReturnsFalseWhenNeitherPathMatches()
    {
        Assert.False(RunningWorkbookObjectTable.WorkbookPathMatchesTarget(
            @"C:\temp\Book1.xlsx",
            @"C:\temp\Book2.xlsx",
            @"C:\temp\Book3.xlsx"));
    }

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
    public void NormalizePath_PreservesWorkbookIdentityShape(string rawPath, string expectedNormalizedPath)
    {
        Assert.Equal(expectedNormalizedPath, RunningWorkbookObjectTable.NormalizePath(rawPath));
    }

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
    public void ComExcelApplicationHandleNormalizePath_PreservesWorkbookIdentityShape(string rawPath, string expectedNormalizedPath)
    {
        Assert.Equal(expectedNormalizedPath, ComExcelApplicationHandle.NormalizePath(rawPath));
    }
}
