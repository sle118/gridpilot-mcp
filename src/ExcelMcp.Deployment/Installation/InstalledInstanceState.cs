namespace ExcelMcp.Deployment.Installation;

public sealed record InstalledInstanceState(
    InstallScope Scope,
    string Version,
    InstallationPaths Paths,
    bool StartupEnabled,
    DateTimeOffset? InstalledAtUtc)
{
    public bool IsInstalled => File.Exists(Paths.MetadataPath) || File.Exists(Paths.TrayExecutablePath);
}
