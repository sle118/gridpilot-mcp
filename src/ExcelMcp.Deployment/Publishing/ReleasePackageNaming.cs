namespace ExcelMcp.Deployment.Publishing;

public static class ReleasePackageNaming
{
    public static string BuildPackageRootName(string version)
    {
        return $"gridpilot-mcp-{NormalizeVersion(version)}-windows-x64";
    }

    public static string BuildArchiveFileName(string version)
    {
        return $"{BuildPackageRootName(version)}.zip";
    }

    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Release version must be provided.", nameof(version));
        }

        return version.Trim();
    }
}
