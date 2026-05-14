using System.Diagnostics;
using System.Reflection;
using System.Text;
using ExcelMcp.Bridge.Contracts;
using ExcelMcp.Core;
using ExcelMcp.Core.Logging;
using ExcelMcp.Deployment.Diagnostics;
using ExcelMcp.Deployment.Logs;
using ExcelMcp.Deployment.Profiles;
using ExcelMcp.ToolHost.Mcp;

namespace ExcelMcp.ToolHost.Diagnostics;

internal sealed class HostRuntimeDiagnosticsService
{
    private readonly HostOptions _options;
    private readonly GridPilotMutableLogger _logger;
    private readonly RuntimeDiagnosticsOverrideStore _overrideStore;
    private readonly IWorkbookServiceResolver _resolver;

    public HostRuntimeDiagnosticsService(
        HostOptions options,
        GridPilotMutableLogger logger,
        RuntimeDiagnosticsOverrideStore overrideStore,
        IWorkbookServiceResolver resolver)
    {
        _options = options;
        _logger = logger;
        _overrideStore = overrideStore;
        _resolver = resolver;
    }

    public async Task<RuntimeDiagnosticsSnapshot> GetRuntimeSnapshotAsync(
        string? clientName,
        string? clientVersion,
        string schemaProfile,
        CancellationToken cancellationToken = default)
    {
        var connections = await _resolver.ListConnectionsAsync(cancellationToken).ConfigureAwait(false);
        var persistentOverride = MapPersistentOverride(_overrideStore.ReadState());
        return new RuntimeDiagnosticsSnapshot(
            SessionMode: NormalizeSessionMode(_options.SessionMode),
            AttachTarget: NormalizeAttachTarget(_options.AttachTarget),
            ClientName: clientName,
            ClientVersion: clientVersion,
            SchemaProfile: schemaProfile,
            EffectiveLogLevel: NormalizeLogLevel(_logger.Level),
            BaseLogLevel: NormalizeLogLevel(_options.BaseLogLevel),
            EffectiveLogPath: _logger.LogPath ?? _options.EffectiveLogPath,
            PersistentOverride: persistentOverride,
            RuntimeLogLevelOverrideSupported: true,
            Connections: connections);
    }

    public IReadOnlyList<DeploymentLogEntry> ListLogs() =>
        DeploymentLogLocator.Locate(BuildRuntimeProfile(), Environment.CurrentDirectory);

    public Task<RecentLogReadResult> ReadLogTailAsync(
        string? path,
        string? kind,
        RecentLogReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = ResolveLogPath(path, kind);
        return RecentLogReader.ReadTailAsync(resolvedPath, options, cancellationToken);
    }

    public async Task<RuntimeDiagnosticReportResult> BuildReportAsync(
        string? clientName,
        string? clientVersion,
        string schemaProfile,
        WorkbookTarget? target = null,
        bool includeRecentLogTails = false,
        RecentLogReadOptions? recentLogOptions = null,
        CancellationToken cancellationToken = default)
    {
        var runtime = await GetRuntimeSnapshotAsync(clientName, clientVersion, schemaProfile, cancellationToken).ConfigureAwait(false);
        SessionDiagnostics? sessionDiagnostics = null;
        if (target is not null)
        {
            sessionDiagnostics = await _resolver.GetSessionDiagnosticsAsync(target, cancellationToken).ConfigureAwait(false);
        }

        var baseReport = await DeploymentDiagnosticReportBuilder.BuildAsync(
            BuildRuntimeProfile(),
            new DeploymentDiagnosticReportOptions
            {
                IncludeRecentLogTails = includeRecentLogTails,
                RecentLogOptions = recentLogOptions ?? RecentLogReadOptions.Default
            },
            Environment.CurrentDirectory,
            cancellationToken).ConfigureAwait(false);

        var builder = new StringBuilder();
        builder.AppendLine("# GridPilot Runtime Diagnostic Report");
        builder.AppendLine();
        builder.AppendLine("## Runtime");
        builder.AppendLine($"- Session mode: {runtime.SessionMode}");
        builder.AppendLine($"- Attach target: {runtime.AttachTarget}");
        builder.AppendLine($"- Client: {clientName ?? "(unknown)"} {clientVersion ?? string.Empty}".TrimEnd());
        builder.AppendLine($"- Schema profile: {schemaProfile}");
        builder.AppendLine($"- Effective log level: {runtime.EffectiveLogLevel.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- Base log level: {runtime.BaseLogLevel.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- Effective log path: {runtime.EffectiveLogPath}");
        builder.AppendLine($"- Persistent override path: {runtime.PersistentOverride.SettingsPath}");
        builder.AppendLine($"- Persistent override level: {runtime.PersistentOverride.LogLevelOverride?.ToString().ToLowerInvariant() ?? "(none)"}");
        builder.AppendLine($"- Connection count: {runtime.Connections.Count}");

        if (sessionDiagnostics is not null)
        {
            builder.AppendLine();
            builder.AppendLine("## Session Diagnostics");
            builder.AppendLine($"- Session mode: {sessionDiagnostics.SessionMode}");
            builder.AppendLine($"- Attach target: {sessionDiagnostics.AttachTargetMode?.ToString() ?? "(none)"}");
            builder.AppendLine($"- Is ready: {sessionDiagnostics.IsReady}");
            builder.AppendLine($"- Is interactive: {sessionDiagnostics.IsInteractive}");
            builder.AppendLine($"- Calculation state: {sessionDiagnostics.CalculationState}");
            builder.AppendLine($"- Is editing cell: {sessionDiagnostics.IsEditingCell}");
            builder.AppendLine($"- Has modal UI: {sessionDiagnostics.HasModalUi}");
            builder.AppendLine($"- Is busy: {sessionDiagnostics.IsBusy}");
        }

        builder.AppendLine();
        builder.AppendLine(baseReport.Content);

        return new RuntimeDiagnosticReportResult(
            builder.ToString().TrimEnd(),
            runtime,
            sessionDiagnostics,
            baseReport.Logs,
            baseReport.RecentLogTails);
    }

    public RuntimeLogLevelChangeResult SetLogLevel(RuntimeLogLevelChangeRequest request)
    {
        var normalizedScope = NormalizeScope(request.Scope);
        var normalizedLevel = NormalizeLevel(request.Level);

        if (normalizedScope is "persistent" or "both")
        {
            if (normalizedLevel is null)
            {
                _overrideStore.ClearLogLevelOverride();
            }
            else
            {
                _overrideStore.WriteLogLevelOverride(normalizedLevel.Value);
            }
        }

        if (normalizedScope is "runtime" or "both")
        {
            var runtimeLevel = normalizedLevel ?? _options.BaseLogLevel;
            _logger.UpdateLevel(runtimeLevel, _options.EffectiveLogPath);
        }

        return new RuntimeLogLevelChangeResult(
            Succeeded: true,
            RequestedLevel: request.Level,
            RequestedScope: normalizedScope,
            EffectiveRuntimeLevel: NormalizeLogLevel(_logger.Level),
            BaseLogLevel: NormalizeLogLevel(_options.BaseLogLevel),
            EffectiveLogPath: _logger.LogPath ?? _options.EffectiveLogPath,
            PersistentOverride: MapPersistentOverride(_overrideStore.ReadState()),
            Message: normalizedLevel is null
                ? "Runtime diagnostics log level reset to default handling."
                : $"Runtime diagnostics log level set to {normalizedLevel.Value.ToString().ToLowerInvariant()}.");
    }

    private string ResolveLogPath(string? path, string? kind)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new WorkbookTargetResolutionException(
                "diagnostics_log_target_required",
                "Reading a log tail requires either 'path' or 'kind'.");
        }

        var normalizedKind = NormalizeLogKind(kind);
        var entry = ListLogs().FirstOrDefault(candidate => candidate.Kind == normalizedKind);
        if (entry is null)
        {
            throw new WorkbookTargetResolutionException(
                "diagnostics_log_kind_not_found",
                $"No log candidate matched '{kind}'.");
        }

        return entry.Path;
    }

    private LaunchProfile BuildRuntimeProfile() =>
        new()
        {
            SchemaVersion = 1,
            Name = "gridpilot-host",
            DisplayName = "GridPilot MCP Host",
            Host = new LaunchProfileHost
            {
                Command = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location,
                Args = Environment.GetCommandLineArgs().Skip(1).ToArray(),
                WorkingDirectory = Environment.CurrentDirectory,
                Env = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["GRIDPILOT_LOG_LEVEL"] = _logger.Level.ToString().ToLowerInvariant(),
                    ["GRIDPILOT_LOG_PATH"] = _logger.LogPath ?? _options.EffectiveLogPath
                }
            },
            Logs = new LaunchProfileLogs
            {
                Path = _logger.LogPath ?? _options.EffectiveLogPath,
                StdoutPolicy = "json-rpc-only"
            },
            Metadata = new LaunchProfileMetadata
            {
                Description = "Live GridPilot MCP host runtime diagnostics."
            }
        };

    private static string NormalizeSessionMode(SessionMode mode) =>
        mode == SessionMode.Attach ? "attach" : "create-new";

    private static string NormalizeAttachTarget(SessionAttachTargetMode mode) =>
        mode == SessionAttachTargetMode.AnyRunningInstance ? "any-running" : "workbook-owner";

    private static string NormalizeScope(string scope)
    {
        var normalized = scope.Trim().ToLowerInvariant();
        return normalized switch
        {
            "runtime" => normalized,
            "persistent" => normalized,
            "both" => normalized,
            _ => throw new McpToolInputException("invalid_arguments", "Scope must be one of 'runtime', 'persistent', or 'both'.")
        };
    }

    private static GridPilotLogLevel? NormalizeLevel(string level)
    {
        var normalized = level.Trim().ToLowerInvariant();
        return normalized switch
        {
            "default" => null,
            "off" => GridPilotLogLevel.Off,
            "info" => GridPilotLogLevel.Info,
            "debug" => GridPilotLogLevel.Debug,
            "trace" => GridPilotLogLevel.Trace,
            _ => throw new McpToolInputException("invalid_arguments", "Level must be one of 'default', 'off', 'info', 'debug', or 'trace'.")
        };
    }

    private static string NormalizeLogLevel(GridPilotLogLevel level) =>
        level.ToString().ToLowerInvariant();

    private static RuntimePersistentOverrideState MapPersistentOverride(RuntimeDiagnosticsSettingsState state) =>
        new(
            state.SettingsPath,
            state.Exists,
            state.LogLevelOverride?.ToString().ToLowerInvariant());

    private static DeploymentLogKind NormalizeLogKind(string kind) =>
        kind.Trim().ToLowerInvariant() switch
        {
            "profile-configured" => DeploymentLogKind.ProfileConfigured,
            "host-env" => DeploymentLogKind.HostEnvironment,
            "host-conventional" => DeploymentLogKind.HostConventional,
            "proxy-conventional" => DeploymentLogKind.ProxyConventional,
            _ => throw new McpToolInputException("invalid_arguments", "Log kind must be one of 'profile-configured', 'host-env', 'host-conventional', or 'proxy-conventional'.")
        };
}
