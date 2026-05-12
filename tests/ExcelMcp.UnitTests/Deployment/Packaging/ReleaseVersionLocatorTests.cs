using System.Reflection;
using System.Text.Json;
using ExcelMcp.Deployment.Publishing;

namespace ExcelMcp.UnitTests.Deployment.Packaging;

public sealed class ReleaseVersionLocatorTests
{
    [Fact]
    public void GetDisplayVersion_UsesReleaseManifestWhenPresent()
    {
        using var workspace = new TempDirectory();
        File.WriteAllText(
            Path.Combine(workspace.Path, "release-manifest.json"),
            JsonSerializer.Serialize(new { version = "v9.9.9-test.1" }));

        var version = ReleaseVersionLocator.GetDisplayVersion(typeof(ReleaseVersionLocatorTests).Assembly, workspace.Path);

        Assert.Equal("v9.9.9-test.1", version);
    }

    [Fact]
    public void GetDisplayVersion_FallsBackToAssemblyVersionWhenManifestMissing()
    {
        using var workspace = new TempDirectory();

        var version = ReleaseVersionLocator.GetDisplayVersion(typeof(ReleaseVersionLocatorTests).Assembly, workspace.Path);

        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
