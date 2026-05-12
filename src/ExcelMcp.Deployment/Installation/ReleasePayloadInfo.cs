namespace ExcelMcp.Deployment.Installation;

public sealed record ReleasePayloadInfo(
    string SourceRoot,
    string Version,
    string ManifestPath,
    string TrayExecutablePath,
    string SetupExecutablePath,
    string HostExecutablePath,
    string ProxyExecutablePath);
