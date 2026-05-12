using ExcelMcp.Deployment.AgentConfig;

namespace ExcelMcp.UnitTests.Deployment.AgentConfig;

public sealed class VsCodeUserMcpConfigPathLocatorTests
{
    [Fact]
    public void ResolvePath_UsesWindowsUserMcpJsonPath()
    {
        var sut = new VsCodeUserMcpConfigPathLocator();

        var path = sut.ResolvePath();

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User", "mcp.json"),
            path);
    }
}
