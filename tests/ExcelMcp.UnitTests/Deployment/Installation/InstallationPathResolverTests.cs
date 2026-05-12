using ExcelMcp.Deployment.Installation;

namespace ExcelMcp.UnitTests.Deployment.Installation;

public sealed class InstallationPathResolverTests
{
    [Fact]
    public void Resolve_PerUser_UsesLocalApplicationDataAppFolder()
    {
        var paths = InstallationPathsResolver.Resolve(InstallScope.PerUser);

        Assert.EndsWith(Path.Combine("GridPilot MCP", "app"), paths.InstallRoot);
        Assert.EndsWith(Path.Combine("GridPilot MCP", "profiles"), paths.ProfileRoot);
        Assert.EndsWith(Path.Combine("GridPilot MCP", "logs"), paths.LogRoot);
        Assert.EndsWith(Path.Combine("GridPilot MCP", "install-state.json"), paths.MetadataPath);
        Assert.EndsWith("GridPilot.Tray.exe", paths.TrayExecutablePath);
    }

    [Fact]
    public void Resolve_MachineWide_UsesProgramFilesButKeepsUserDataUserScoped()
    {
        var paths = InstallationPathsResolver.Resolve(InstallScope.MachineWide);

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GridPilot MCP"),
            paths.InstallRoot);
        Assert.EndsWith(Path.Combine("GridPilot MCP", "profiles"), paths.ProfileRoot);
        Assert.EndsWith(Path.Combine("GridPilot MCP", "logs"), paths.LogRoot);
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GridPilot MCP", "install-state.json"),
            paths.MetadataPath);
    }
}
