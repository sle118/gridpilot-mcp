using ExcelMcp.Deployment.Publishing;

namespace ExcelMcp.UnitTests.Deployment.Packaging;

public sealed class ReleasePackageNamingTests
{
    [Theory]
    [InlineData("v1.2.3", "gridpilot-mcp-v1.2.3-windows-x64", "gridpilot-mcp-v1.2.3-windows-x64.zip")]
    [InlineData("v1.2.3-beta.1", "gridpilot-mcp-v1.2.3-beta.1-windows-x64", "gridpilot-mcp-v1.2.3-beta.1-windows-x64.zip")]
    public void BuildArchiveFileName_UsesVersionedWindowsPackageName(string version, string packageRootName, string archiveFileName)
    {
        Assert.Equal(packageRootName, ReleasePackageNaming.BuildPackageRootName(version));
        Assert.Equal(archiveFileName, ReleasePackageNaming.BuildArchiveFileName(version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildArchiveFileName_RejectsMissingVersion(string? version)
    {
        Assert.Throws<ArgumentException>(() => ReleasePackageNaming.BuildArchiveFileName(version!));
    }
}
