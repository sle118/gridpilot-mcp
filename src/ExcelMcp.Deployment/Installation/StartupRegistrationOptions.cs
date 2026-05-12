namespace ExcelMcp.Deployment.Installation;

public sealed record StartupRegistrationOptions(
    InstallScope Scope,
    string TrayExecutablePath,
    IReadOnlyList<string> Arguments);
