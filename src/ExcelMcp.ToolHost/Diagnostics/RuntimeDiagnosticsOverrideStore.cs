using System.Text.Json;
using ExcelMcp.Core.Logging;

namespace ExcelMcp.ToolHost.Diagnostics;

internal sealed class RuntimeDiagnosticsOverrideStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsPath { get; }

    public RuntimeDiagnosticsOverrideStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public RuntimeDiagnosticsSettingsState ReadState()
    {
        if (!File.Exists(SettingsPath))
        {
            return new RuntimeDiagnosticsSettingsState(SettingsPath, Exists: false, LogLevelOverride: null);
        }

        try
        {
            var settings = JsonSerializer.Deserialize<RuntimeDiagnosticsSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                ?? new RuntimeDiagnosticsSettings(null);
            return new RuntimeDiagnosticsSettingsState(SettingsPath, Exists: true, settings.LogLevelOverride);
        }
        catch
        {
            return new RuntimeDiagnosticsSettingsState(SettingsPath, Exists: true, LogLevelOverride: null);
        }
    }

    public void WriteLogLevelOverride(GridPilotLogLevel level)
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var settings = new RuntimeDiagnosticsSettings(level);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public void ClearLogLevelOverride()
    {
        if (File.Exists(SettingsPath))
        {
            File.Delete(SettingsPath);
        }
    }

    public static string GetDefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "GridPilot MCP", "diagnostics", "runtime-settings.json");
    }
}
