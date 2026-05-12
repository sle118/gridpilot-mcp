namespace ExcelMcp.Deployment.Installation;

public static class InstallationPathsResolver
{
    public const string ProductFolderName = "GridPilot MCP";
    public const string TrayExecutableFileName = "GridPilot.Tray.exe";
    public const string SetupExecutableFileName = "GridPilot.Setup.exe";
    public const string HostExecutableFileName = "ExcelMcp.ToolHost.exe";
    public const string ProxyExecutableFileName = "ExcelMcp.ToolProxy.exe";
    public const string DefaultProfileFileName = "gridpilot-default.json";
    public const string MetadataFileName = "install-state.json";
    public const string StartMenuShortcutFileName = "GridPilot MCP.lnk";

    public static InstallationPaths Resolve(InstallScope scope)
    {
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName);
        var installRoot = scope == InstallScope.MachineWide
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), ProductFolderName)
            : Path.Combine(localRoot, "app");
        var startMenuProgramsRoot = Path.Combine(
            Environment.GetFolderPath(scope == InstallScope.MachineWide
                ? Environment.SpecialFolder.CommonPrograms
                : Environment.SpecialFolder.Programs),
            ProductFolderName);
        var metadataRoot = scope == InstallScope.MachineWide
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProductFolderName)
            : localRoot;
        var profileRoot = Path.Combine(localRoot, "profiles");
        var logRoot = Path.Combine(localRoot, "logs");

        return new InstallationPaths(
            InstallRoot: installRoot,
            ProfileRoot: profileRoot,
            LogRoot: logRoot,
            StartMenuProgramsRoot: startMenuProgramsRoot,
            TrayExecutablePath: Path.Combine(installRoot, TrayExecutableFileName),
            SetupExecutablePath: Path.Combine(installRoot, SetupExecutableFileName),
            HostExecutablePath: Path.Combine(installRoot, "host", HostExecutableFileName),
            ProxyExecutablePath: Path.Combine(installRoot, "proxy", ProxyExecutableFileName),
            DefaultProfilePath: Path.Combine(profileRoot, DefaultProfileFileName),
            MetadataPath: Path.Combine(metadataRoot, MetadataFileName));
    }
}
