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

    [Fact]
    public void EnsureDefaultProfile_UsesSharedInstalledHostLaunchDefaults()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        var launchDefaults = InstalledHostLaunchDefaultsBuilder.Build(install);

        var profilePath = new ProfileBootstrapService().EnsureDefaultProfile(install);
        var loaded = LaunchProfileLoader.Load(profilePath);

        Assert.True(loaded.IsSuccess);
        Assert.Equal(launchDefaults.Command, loaded.Profile?.Host?.Command);
        Assert.Equal(launchDefaults.Args, loaded.Profile?.Host?.Args);
        Assert.Equal(launchDefaults.RuntimeLogPath, loaded.Profile?.Logs?.Path);
        Assert.Equal(launchDefaults.Env["GRIDPILOT_LOG_LEVEL"], loaded.Profile?.Host?.Env?["GRIDPILOT_LOG_LEVEL"]);
        Assert.Equal(launchDefaults.Env["GRIDPILOT_LOG_PATH"], loaded.Profile?.Host?.Env?["GRIDPILOT_LOG_PATH"]);
    }
}
