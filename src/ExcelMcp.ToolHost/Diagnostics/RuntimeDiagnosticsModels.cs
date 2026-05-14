using ExcelMcp.Core;
using ExcelMcp.Deployment.Diagnostics;
using ExcelMcp.Deployment.Logs;

namespace ExcelMcp.ToolHost.Diagnostics;

internal sealed record RuntimeDiagnosticsSnapshot(
    string SessionMode,
    string AttachTarget,
    string? ClientName,
    string? ClientVersion,
    string SchemaProfile,
    string EffectiveLogLevel,
    string BaseLogLevel,
    string EffectiveLogPath,
    RuntimePersistentOverrideState PersistentOverride,
    bool RuntimeLogLevelOverrideSupported,
    IReadOnlyList<WorkbookConnectionInfo> Connections);

internal sealed record RuntimePersistentOverrideState(
    string SettingsPath,
    bool Exists,
    string? LogLevelOverride);

internal sealed record RuntimeLogLevelChangeRequest(
    string Level,
    string Scope);

internal sealed record RuntimeLogLevelChangeResult(
    bool Succeeded,
    string RequestedLevel,
    string RequestedScope,
    string EffectiveRuntimeLevel,
    string BaseLogLevel,
    string EffectiveLogPath,
    RuntimePersistentOverrideState PersistentOverride,
    string? Message = null);

internal sealed record RuntimeDiagnosticReportResult(
    string Content,
    RuntimeDiagnosticsSnapshot Runtime,
    SessionDiagnostics? SessionDiagnostics,
    IReadOnlyList<DeploymentLogEntry> Logs,
    IReadOnlyList<RecentLogReadResult> RecentLogTails);
