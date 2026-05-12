using System.Reflection;
using System.Text.Json;

namespace ExcelMcp.Deployment.Publishing;

public static class ReleaseVersionLocator
{
    private const string ManifestFileName = "release-manifest.json";

    public static string GetDisplayVersion(Assembly assembly, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var manifestVersion = TryReadManifestVersion(baseDirectory ?? AppContext.BaseDirectory);
        if (!string.IsNullOrWhiteSpace(manifestVersion))
        {
            return manifestVersion;
        }

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string? TryReadManifestVersion(string baseDirectory)
    {
        var manifestPath = Path.Combine(baseDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("version", out var versionElement) &&
                   versionElement.ValueKind == JsonValueKind.String
                ? versionElement.GetString()
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
