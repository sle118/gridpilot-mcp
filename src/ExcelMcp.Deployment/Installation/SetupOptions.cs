namespace ExcelMcp.Deployment.Installation;

public sealed record SetupOptions(
    InstallScope Scope,
    string SourceRoot,
    bool StartupEnabled,
    bool CreateStartMenuShortcut);
