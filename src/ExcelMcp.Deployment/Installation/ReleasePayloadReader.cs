using System.Text.Json;

namespace ExcelMcp.Deployment.Installation;

public static class ReleasePayloadReader
{
    private const string ManifestFileName = "release-manifest.json";

    public static ReleasePayloadInfo Read(string sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            throw new InvalidOperationException("Release payload source root is required.");
        }

        var fullSourceRoot = Path.GetFullPath(sourceRoot);
        if (!Directory.Exists(fullSourceRoot))
        {
            throw new InvalidOperationException($"Release payload folder does not exist: {fullSourceRoot}");
        }

        var trayExecutablePath = Path.Combine(fullSourceRoot, InstallationPathsResolver.TrayExecutableFileName);
        var setupExecutablePath = Path.Combine(fullSourceRoot, InstallationPathsResolver.SetupExecutableFileName);
        var hostExecutablePath = Path.Combine(fullSourceRoot, "host", InstallationPathsResolver.HostExecutableFileName);
        var proxyExecutablePath = Path.Combine(fullSourceRoot, "proxy", InstallationPathsResolver.ProxyExecutableFileName);
        var manifestPath = Path.Combine(fullSourceRoot, ManifestFileName);

        EnsureFileExists(trayExecutablePath, "tray executable");
        EnsureFileExists(setupExecutablePath, "setup executable");
        EnsureFileExists(hostExecutablePath, "host executable");
        EnsureFileExists(proxyExecutablePath, "proxy executable");
        EnsureFileExists(manifestPath, "release manifest");

        var manifest = JsonSerializer.Deserialize<ReleaseManifestModel>(File.ReadAllText(manifestPath));
        var version = string.IsNullOrWhiteSpace(manifest?.Version)
            ? "unknown"
            : manifest.Version.Trim();

        return new ReleasePayloadInfo(
            fullSourceRoot,
            version,
            manifestPath,
            trayExecutablePath,
            setupExecutablePath,
            hostExecutablePath,
            proxyExecutablePath);
    }

    private static void EnsureFileExists(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Release payload is missing the {label}: {path}");
        }
    }
}
