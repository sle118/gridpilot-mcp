namespace ExcelMcp.Deployment.Installation;

public interface IInstallationPathResolver
{
    InstallationPaths Resolve(InstallScope scope);
}
