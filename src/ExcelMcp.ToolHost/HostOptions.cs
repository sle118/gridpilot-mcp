using ExcelMcp.Core;
using ExcelMcp.Core.Logging;
using ExcelMcp.ToolHost.Diagnostics;

namespace ExcelMcp.ToolHost;

internal enum SessionMode
{
    CreateNew = 0,
    Attach = 1
}

internal sealed record HostOptions(
    SessionMode SessionMode,
    SessionAttachTargetMode AttachTarget,
    bool Visible,
    GridPilotLogLevel LogLevel,
    string? LogPath)
{
    public GridPilotLogLevel BaseLogLevel { get; init; } = LogLevel;

    public string EffectiveLogPath { get; init; } = LogPath ?? Path.Combine(Environment.CurrentDirectory, ".tmp", "gridpilot-runtime.log");

    public string RuntimeDiagnosticsSettingsPath { get; init; } = RuntimeDiagnosticsOverrideStore.GetDefaultSettingsPath();

    public GridPilotLogLevel? PersistentLogLevelOverride { get; init; }

    public bool PersistentLogLevelOverrideApplied { get; init; }

    public static HostOptions Parse(string[] args) =>
        Parse(args, null);

    internal static HostOptions Parse(string[] args, RuntimeDiagnosticsOverrideStore? diagnosticsOverrideStore)
    {
        var mode = ReadModeFromEnvironment();
        var attachTarget = ReadAttachTargetFromEnvironment();
        var visible = ReadVisibleFromEnvironment();
        var baseLogLevel = ReadLogLevelFromEnvironment();
        var logPath = ReadLogPathFromEnvironment();
        var logLevelSpecifiedByArgs = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--visible", StringComparison.OrdinalIgnoreCase))
            {
                visible = true;
                continue;
            }

            if (string.Equals(argument, "--session-mode", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                index++;
                mode = ParseMode(args[index]);
                continue;
            }

            if (string.Equals(argument, "--attach-target", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                index++;
                attachTarget = ParseAttachTarget(args[index]);
                continue;
            }

            if (string.Equals(argument, "--log-level", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                index++;
                baseLogLevel = ParseLogLevel(args[index]);
                logLevelSpecifiedByArgs = true;
                continue;
            }

            if (string.Equals(argument, "--log-path", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                index++;
                logPath = args[index];
                continue;
            }
        }

        var settingsStore = diagnosticsOverrideStore ?? new RuntimeDiagnosticsOverrideStore();
        var overrideState = settingsStore.ReadState();
        var effectiveLogLevel = !logLevelSpecifiedByArgs && overrideState.LogLevelOverride is not null
            ? overrideState.LogLevelOverride.Value
            : baseLogLevel;
        var effectiveLogPath = string.IsNullOrWhiteSpace(logPath)
            ? System.IO.Path.Combine(Environment.CurrentDirectory, ".tmp", "gridpilot-runtime.log")
            : logPath!;

        if (effectiveLogLevel != GridPilotLogLevel.Off && string.IsNullOrWhiteSpace(logPath))
        {
            logPath = effectiveLogPath;
        }

        return new HostOptions(mode, attachTarget, visible, effectiveLogLevel, logPath)
        {
            BaseLogLevel = baseLogLevel,
            EffectiveLogPath = effectiveLogPath,
            RuntimeDiagnosticsSettingsPath = settingsStore.SettingsPath,
            PersistentLogLevelOverride = overrideState.LogLevelOverride,
            PersistentLogLevelOverrideApplied = !logLevelSpecifiedByArgs && overrideState.LogLevelOverride is not null
        };
    }

    public string ToStartupSummary() =>
        $"GridPilot MCP starting with sessionMode={SessionModeToString(SessionMode)}, attachTarget={AttachTargetToString(AttachTarget)}, visible={Visible.ToString().ToLowerInvariant()}, logLevel={LogLevelToString(LogLevel)}, logPath={LogPath ?? "(none)"}, baseLogLevel={LogLevelToString(BaseLogLevel)}, persistentLogLevelOverride={PersistentLogLevelOverride?.ToString().ToLowerInvariant() ?? "(none)"}.";

    private static SessionMode ParseMode(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "attach" => SessionMode.Attach,
            "create-new" => SessionMode.CreateNew,
            "new" => SessionMode.CreateNew,
            _ => throw new InvalidOperationException($"Unsupported session mode '{value}'. Use 'attach' or 'create-new'.")
        };

    private static SessionMode ReadModeFromEnvironment()
    {
        var envMode = Environment.GetEnvironmentVariable("GRIDPILOT_SESSION_MODE");
        return string.IsNullOrWhiteSpace(envMode) ? SessionMode.CreateNew : ParseMode(envMode);
    }

    private static SessionAttachTargetMode ParseAttachTarget(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "any" => SessionAttachTargetMode.AnyRunningInstance,
            "any-running" => SessionAttachTargetMode.AnyRunningInstance,
            "workbook" => SessionAttachTargetMode.WorkbookOwner,
            "workbook-owner" => SessionAttachTargetMode.WorkbookOwner,
            _ => throw new InvalidOperationException($"Unsupported attach target '{value}'. Use 'workbook-owner' or 'any-running'.")
        };

    private static SessionAttachTargetMode ReadAttachTargetFromEnvironment()
    {
        var envTarget = Environment.GetEnvironmentVariable("GRIDPILOT_ATTACH_TARGET");
        return string.IsNullOrWhiteSpace(envTarget) ? SessionAttachTargetMode.WorkbookOwner : ParseAttachTarget(envTarget);
    }

    private static bool ReadVisibleFromEnvironment()
    {
        var envVisible = Environment.GetEnvironmentVariable("GRIDPILOT_SESSION_VISIBLE");
        return string.Equals(envVisible, "1", StringComparison.Ordinal);
    }

    private static GridPilotLogLevel ParseLogLevel(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "off" => GridPilotLogLevel.Off,
            "info" => GridPilotLogLevel.Info,
            "debug" => GridPilotLogLevel.Debug,
            "trace" => GridPilotLogLevel.Trace,
            _ => throw new InvalidOperationException($"Unsupported log level '{value}'. Use 'off', 'info', 'debug', or 'trace'.")
        };

    private static GridPilotLogLevel ReadLogLevelFromEnvironment()
    {
        var envLevel = Environment.GetEnvironmentVariable("GRIDPILOT_LOG_LEVEL");
        return string.IsNullOrWhiteSpace(envLevel) ? GridPilotLogLevel.Off : ParseLogLevel(envLevel);
    }

    private static string? ReadLogPathFromEnvironment() =>
        Environment.GetEnvironmentVariable("GRIDPILOT_LOG_PATH");

    private static string SessionModeToString(SessionMode mode) =>
        mode switch
        {
            SessionMode.Attach => "attach",
            _ => "create-new"
        };

    private static string AttachTargetToString(SessionAttachTargetMode target) =>
        target switch
        {
            SessionAttachTargetMode.AnyRunningInstance => "any-running",
            _ => "workbook-owner"
        };

    private static string LogLevelToString(GridPilotLogLevel level) =>
        level.ToString().ToLowerInvariant();
}
