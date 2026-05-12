namespace ExcelMcp.Deployment.Installation;

public sealed record SetupPlan(
    SetupOperationKind Operation,
    SetupOptions Options,
    string ReleaseVersion,
    InstallationPaths Paths,
    InstalledInstanceState? ExistingInstall,
    bool RequiresElevation,
    IReadOnlyList<string> PreviewLines);
