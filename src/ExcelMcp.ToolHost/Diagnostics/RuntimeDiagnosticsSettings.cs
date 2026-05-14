using System.Text.Json.Serialization;
using ExcelMcp.Core.Logging;

namespace ExcelMcp.ToolHost.Diagnostics;

internal sealed record RuntimeDiagnosticsSettings(
    [property: JsonConverter(typeof(JsonStringEnumConverter<GridPilotLogLevel>))] GridPilotLogLevel? LogLevelOverride);

internal sealed record RuntimeDiagnosticsSettingsState(
    string SettingsPath,
    bool Exists,
    [property: JsonConverter(typeof(JsonStringEnumConverter<GridPilotLogLevel>))] GridPilotLogLevel? LogLevelOverride);
