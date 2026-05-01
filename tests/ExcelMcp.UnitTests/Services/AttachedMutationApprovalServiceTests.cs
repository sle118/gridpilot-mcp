using ExcelMcp.Bridge.Services;

namespace ExcelMcp.UnitTests.Services;

public sealed class AttachedMutationApprovalServiceTests
{
    [Fact]
    public async Task GrantAsync_PreservesUrlStyleWorkbookIdentityInResponse()
    {
        var registry = new InMemoryAttachedMutationApprovalRegistry(() => new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        var sut = new AttachedMutationApprovalService(registry);

        var result = await sut.GrantAsync("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx");

        Assert.True(result.Succeeded);
        Assert.Equal("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx", result.WorkbookPath);
    }

    [Fact]
    public async Task RevokeAsync_PreservesUrlStyleWorkbookIdentityInResponse()
    {
        var registry = new InMemoryAttachedMutationApprovalRegistry(() => new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        var sut = new AttachedMutationApprovalService(registry);
        await sut.GrantAsync("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx");

        var result = await sut.RevokeAsync("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx");

        Assert.True(result.Succeeded);
        Assert.Equal("https://d.docs.live.net/171321e0a36cf836/Documents/Book_mcp_test.xlsx", result.WorkbookPath);
    }
}
