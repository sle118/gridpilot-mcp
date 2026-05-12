namespace ExcelMcp.Deployment.Installation;

public sealed class DefaultInstallationPathResolver : IInstallationPathResolver
{
    public InstallationPaths Resolve(InstallScope scope) => InstallationPathsResolver.Resolve(scope);
}
