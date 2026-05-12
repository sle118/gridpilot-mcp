using System.Text.Json;

namespace ExcelMcp.Deployment.Doctor;

public sealed class RuntimeConfigReader : IRuntimeConfigReader
{
    public RuntimeConfigInfo Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("runtimeOptions", out var runtimeOptions))
        {
            return new RuntimeConfigInfo(null, null);
        }

        if (runtimeOptions.TryGetProperty("framework", out var framework))
        {
            return ReadFramework(framework, usesIncludedFrameworks: false);
        }

        if (runtimeOptions.TryGetProperty("includedFrameworks", out var includedFrameworks) &&
            includedFrameworks.ValueKind == JsonValueKind.Array)
        {
            foreach (var includedFramework in includedFrameworks.EnumerateArray())
            {
                if (includedFramework.ValueKind == JsonValueKind.Object)
                {
                    return ReadFramework(includedFramework, usesIncludedFrameworks: true);
                }
            }
        }

        return new RuntimeConfigInfo(null, null);
    }

    private static RuntimeConfigInfo ReadFramework(JsonElement framework, bool usesIncludedFrameworks)
    {
        var frameworkName = framework.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()
            : null;
        var frameworkVersion = framework.TryGetProperty("version", out var versionElement)
            ? versionElement.GetString()
            : null;

        return new RuntimeConfigInfo(frameworkName, frameworkVersion, usesIncludedFrameworks);
    }
}
