using ExcelMcp.Deployment.Installation;

namespace ExcelMcp.UnitTests.Deployment.Installation;

public sealed class InstallationServiceTests
{
    [Fact]
    public void BuildPlan_ReturnsInstallWhenNoExistingStateExists()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var sut = workspace.CreateInstallationService();

        var plan = sut.BuildPlan(new SetupOptions(InstallScope.PerUser, workspace.ReleaseRoot, StartupEnabled: true, CreateStartMenuShortcut: true), isElevated: false);

        Assert.Equal(SetupOperationKind.Install, plan.Operation);
        Assert.False(plan.RequiresElevation);
    }

    [Fact]
    public async Task BuildPlan_ReturnsRepairWhenVersionMatchesExistingInstall()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var sut = workspace.CreateInstallationService();
        var initialPlan = sut.BuildPlan(new SetupOptions(InstallScope.PerUser, workspace.ReleaseRoot, StartupEnabled: true, CreateStartMenuShortcut: true), isElevated: false);
        await sut.ApplyPlanAsync(initialPlan);

        var plan = sut.BuildPlan(new SetupOptions(InstallScope.PerUser, workspace.ReleaseRoot, StartupEnabled: true, CreateStartMenuShortcut: true), isElevated: false);

        Assert.Equal(SetupOperationKind.Repair, plan.Operation);
    }

    [Fact]
    public async Task BuildPlan_ReturnsUpdateWhenVersionChanges()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var sut = workspace.CreateInstallationService();
        var initialPlan = sut.BuildPlan(new SetupOptions(InstallScope.PerUser, workspace.ReleaseRoot, StartupEnabled: true, CreateStartMenuShortcut: true), isElevated: false);
        await sut.ApplyPlanAsync(initialPlan);

        workspace.SetReleaseVersion("v2.0.0");
        var plan = sut.BuildPlan(new SetupOptions(InstallScope.PerUser, workspace.ReleaseRoot, StartupEnabled: true, CreateStartMenuShortcut: true), isElevated: false);

        Assert.Equal(SetupOperationKind.Update, plan.Operation);
        Assert.Equal("v2.0.0", plan.ReleaseVersion);
    }

    [Fact]
    public async Task ApplyPlan_CopiesPayloadWritesMetadataAndStartup()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var startup = new StartupRegistrationService(workspace.RegistryStore);
        var sut = workspace.CreateInstallationService(startup);
        var plan = sut.BuildPlan(new SetupOptions(InstallScope.PerUser, workspace.ReleaseRoot, StartupEnabled: true, CreateStartMenuShortcut: true), isElevated: false);

        var state = await sut.ApplyPlanAsync(plan);

        Assert.True(File.Exists(state.Paths.TrayExecutablePath));
        Assert.True(File.Exists(state.Paths.SetupExecutablePath));
        Assert.True(File.Exists(state.Paths.HostExecutablePath));
        Assert.True(File.Exists(state.Paths.ProxyExecutablePath));
        Assert.True(File.Exists(state.Paths.MetadataPath));
        Assert.True(state.StartupEnabled);
        Assert.Single(workspace.ShortcutWriter.CreatedShortcuts);
    }

    [Fact]
    public async Task Uninstall_RemovesInstalledFilesButPreservesProfilesAndLogs()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var sut = workspace.CreateInstallationService();
        var plan = sut.BuildPlan(new SetupOptions(InstallScope.PerUser, workspace.ReleaseRoot, StartupEnabled: false, CreateStartMenuShortcut: true), isElevated: false);
        var state = await sut.ApplyPlanAsync(plan);
        Directory.CreateDirectory(state.Paths.ProfileRoot);
        Directory.CreateDirectory(state.Paths.LogRoot);
        File.WriteAllText(Path.Combine(state.Paths.ProfileRoot, "keep.txt"), "profile");
        File.WriteAllText(Path.Combine(state.Paths.LogRoot, "keep.log"), "log");

        await sut.UninstallAsync(InstallScope.PerUser);

        Assert.False(Directory.Exists(state.Paths.InstallRoot));
        Assert.False(File.Exists(state.Paths.MetadataPath));
        Assert.True(File.Exists(Path.Combine(state.Paths.ProfileRoot, "keep.txt")));
        Assert.True(File.Exists(Path.Combine(state.Paths.LogRoot, "keep.log")));
    }
}
