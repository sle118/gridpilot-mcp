using ExcelMcp.Deployment.Installation;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.UnitTests.Deployment.Installation;

public sealed class ProfileBootstrapServiceTests
{
    [Fact]
    public void EnsureDefaultProfile_CreatesInstalledProfileWhenMissing()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        var sut = new ProfileBootstrapService();

        var profilePath = sut.EnsureDefaultProfile(install);
        var loaded = LaunchProfileLoader.Load(profilePath);

        Assert.True(loaded.IsSuccess);
        Assert.Equal(install.Paths.HostExecutablePath, loaded.Profile?.Host?.Command);
        Assert.Equal(profilePath, install.Paths.DefaultProfilePath);
        Assert.Equal("jsonRpcOnly", loaded.Profile?.Logs?.StdoutPolicy);
        Assert.Null(loaded.Profile?.Host?.WorkingDirectory);
    }

    [Fact]
    public void EnsureDefaultProfile_PreservesExistingProfile()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        Directory.CreateDirectory(install.Paths.ProfileRoot);
        File.WriteAllText(install.Paths.DefaultProfilePath, "{\"custom\":true}");

        var profilePath = new ProfileBootstrapService().EnsureDefaultProfile(install);

        Assert.Equal(install.Paths.DefaultProfilePath, profilePath);
        Assert.Equal("{\"custom\":true}", File.ReadAllText(profilePath));
    }
}
