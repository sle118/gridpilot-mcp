namespace ExcelMcp.Deployment.Installation;

public sealed record InstallationPaths(
    string InstallRoot,
    string ProfileRoot,
    string LogRoot,
    string StartMenuProgramsRoot,
    string TrayExecutablePath,
    string SetupExecutablePath,
    string HostExecutablePath,
    string ProxyExecutablePath,
    string DefaultProfilePath,
    string MetadataPath);
