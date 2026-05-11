using System.Text.Json;

namespace ExcelMcp.Deployment.Doctor;

public sealed class RuntimeConfigReader : IRuntimeConfigReader
{
    public RuntimeConfigInfo Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("runtimeOptions", out var runtimeOptions) ||
            !runtimeOptions.TryGetProperty("framework", out var framework))
        {
            return new RuntimeConfigInfo(null, null);
        }

        var frameworkName = framework.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()
            : null;
        var frameworkVersion = framework.TryGetProperty("version", out var versionElement)
            ? versionElement.GetString()
            : null;

        return new RuntimeConfigInfo(frameworkName, frameworkVersion);
    }
}
