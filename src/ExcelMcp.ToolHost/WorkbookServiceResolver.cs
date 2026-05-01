using ExcelMcp.Bridge.Services;
using ExcelMcp.ComAdapter;
using ExcelMcp.ComAdapter.Interop;
using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Logging;
using ExcelMcp.Core.Results;
using System.Runtime.Versioning;
using System.Text.Json;

namespace ExcelMcp.ToolHost;

[SupportedOSPlatform("windows")]
internal sealed class WorkbookServiceResolver : IWorkbookServiceResolver, IAsyncDisposable
{
    private readonly HostOptions _options;
    private readonly object _gate = new();
    private readonly IAttachedMutationApprovalRegistry _approvalRegistry;
    private readonly AttachedMutationApprovalService _approvalService;
    private readonly IGridPilotLogger _logger;
    private readonly Dictionary<string, ConnectedWorkbookConnection> _connectionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _connectionIdsByPath = new(StringComparer.OrdinalIgnoreCase);
    private IExcelSession? _defaultSharedSession;
    private WorkbookService? _defaultSharedService;
    private IExcelSession? _bridgeOwnedSession;
    private WorkbookService? _bridgeOwnedService;

    private WorkbookServiceResolver(HostOptions options, IGridPilotLogger logger)
    {
        _options = options;
        _logger = logger;
        _approvalRegistry = new InMemoryAttachedMutationApprovalRegistry();
        _approvalService = new AttachedMutationApprovalService(_approvalRegistry);
    }

    public static Task<WorkbookServiceResolver> CreateAsync(HostOptions options, IGridPilotLogger logger) =>
        Task.FromResult(new WorkbookServiceResolver(options, logger));

    public async Task<T> ExecuteAsync<T>(
        WorkbookTarget target,
        Func<ResolvedWorkbookContext, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var resolved = await ResolveTargetAsync(target, cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(resolved).ConfigureAwait(false);
        }
        finally
        {
            if (resolved is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workbooks = RunningWorkbookObjectTable.ListRunningWorkbooks(_logger);
        _logger.LogDebug(nameof(WorkbookServiceResolver), "list_open_workbooks", new Dictionary<string, object?>
        {
            ["count"] = workbooks.Count
        });
        return Task.FromResult(workbooks);
    }

    public async Task<WorkbookConnectionResult> ConnectAsync(
        WorkbookConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var explicitPath = NormalizePathOrNull(request.WorkbookPath);
        if (explicitPath is not null)
        {
            _logger.LogInfo(nameof(WorkbookServiceResolver), "connect_requested", new Dictionary<string, object?>
            {
                ["workbookPath"] = explicitPath,
                ["workbookName"] = request.WorkbookName
            });
            var openWorkbooks = RunningWorkbookObjectTable.ListRunningWorkbooks(_logger);
            var existingOpen = openWorkbooks.FirstOrDefault(workbook =>
                string.Equals(workbook.FullPath, explicitPath, StringComparison.OrdinalIgnoreCase));

            if (existingOpen is not null)
            {
                return await ConnectAttachedAsync(existingOpen, reuseForPath: explicitPath, cancellationToken).ConfigureAwait(false);
            }

            return await ConnectBridgeOwnedAsync(
                workbookPath: explicitPath,
                workbookName: Path.GetFileName(explicitPath),
                cancellationToken).ConfigureAwait(false);
        }

        var workbookName = request.WorkbookName?.Trim();
        if (string.IsNullOrWhiteSpace(workbookName))
        {
            throw new WorkbookTargetResolutionException(
                "workbook_target_required",
                "Connecting a workbook requires either 'workbookPath' or 'workbookName'.");
        }

        var matches = RunningWorkbookObjectTable.ListRunningWorkbooks(_logger)
            .Where(workbook => string.Equals(workbook.Name, workbookName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _logger.LogDebug(nameof(WorkbookServiceResolver), "connect_name_lookup", new Dictionary<string, object?>
        {
            ["workbookName"] = workbookName,
            ["matchCount"] = matches.Length
        });

        if (matches.Length == 0)
        {
            throw new WorkbookTargetResolutionException(
                "workbook_name_not_found",
                $"No open Excel workbook matched '{workbookName}'.",
                "Use session_list_open_workbooks to inspect available workbook titles, or provide a full workbook path to open it in a bridge-owned session.");
        }

        if (matches.Length > 1)
        {
            throw new WorkbookTargetResolutionException(
                "workbook_name_ambiguous",
                $"Multiple open Excel workbooks matched '{workbookName}'.",
                JsonSerializer.Serialize(matches.Select(workbook => new { workbook.Name, workbook.FullPath, workbook.IsActive })));
        }

        return await ConnectAttachedAsync(matches[0], reuseForPath: matches[0].FullPath, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<WorkbookConnectionInfo>>(
                _connectionsById.Values
                    .Select(connection => connection.ToInfo(GetApprovalStatus(connection)))
                    .OrderBy(connection => connection.WorkbookName, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
    }

    public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_connectionsById.TryGetValue(connectionId, out var connection))
            {
                return Task.FromResult(connection.ToInfo(GetApprovalStatus(connection)));
            }
        }

        throw new WorkbookTargetResolutionException(
            "connection_not_found",
            $"No workbook connection with id '{connectionId}' exists.");
    }

    public async Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ConnectedWorkbookConnection? removed = null;
        lock (_gate)
        {
            if (_connectionsById.TryGetValue(connectionId, out removed))
            {
                _connectionsById.Remove(connectionId);
                _connectionIdsByPath.Remove(removed.WorkbookPath);
            }
        }

        if (removed is null)
        {
            _logger.LogDebug(nameof(WorkbookServiceResolver), "disconnect_connection_missing", new Dictionary<string, object?>
            {
                ["connectionId"] = connectionId
            });
            return new WorkbookDisconnectResult(true, connectionId, string.Empty, false);
        }

        if (removed.OwnsSession)
        {
            await removed.Session.DisposeAsync().ConfigureAwait(false);
        }

        _logger.LogInfo(nameof(WorkbookServiceResolver), "disconnect_connection", new Dictionary<string, object?>
        {
            ["connectionId"] = connectionId,
            ["workbookPath"] = removed.WorkbookPath,
            ["connectionMode"] = removed.ConnectionMode
        });

        return new WorkbookDisconnectResult(true, connectionId, removed.WorkbookPath, true);
    }

    public async Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(
        WorkbookTarget target,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveTargetAsync(target, cancellationToken).ConfigureAwait(false);
        await EnsureAttachedWorkbookOwnerConnectionAsync(resolved, cancellationToken).ConfigureAwait(false);
        return await _approvalService.GrantAsync(resolved.WorkbookPath, ttl, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(
        WorkbookTarget target,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveTargetAsync(target, cancellationToken).ConfigureAwait(false);
        return await _approvalService.RevokeAsync(resolved.WorkbookPath, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        List<IExcelSession> sessionsToDispose;
        lock (_gate)
        {
            sessionsToDispose =
            [
                .. _connectionsById.Values.Where(connection => connection.OwnsSession).Select(connection => connection.Session),
                .. EnumerateNonNullSharedSessions()
            ];

            _connectionsById.Clear();
            _connectionIdsByPath.Clear();
            _defaultSharedSession = null;
            _defaultSharedService = null;
            _bridgeOwnedSession = null;
            _bridgeOwnedService = null;
        }

        foreach (var session in sessionsToDispose.Distinct(ReferenceEqualityComparer<IExcelSession>.Instance))
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }

        _logger.LogInfo(nameof(WorkbookServiceResolver), "resolver_disposed", new Dictionary<string, object?>
        {
            ["disposedSessionCount"] = sessionsToDispose.Count
        });
    }

    private IEnumerable<IExcelSession> EnumerateNonNullSharedSessions()
    {
        if (_defaultSharedSession is not null)
        {
            yield return _defaultSharedSession;
        }

        if (_bridgeOwnedSession is not null && !ReferenceEquals(_bridgeOwnedSession, _defaultSharedSession))
        {
            yield return _bridgeOwnedSession;
        }
    }

    private async Task<WorkbookConnectionResult> ConnectAttachedAsync(
        WorkbookSummary workbook,
        string reuseForPath,
        CancellationToken cancellationToken)
    {
        if (TryGetExistingConnection(reuseForPath, out var existing))
        {
            _logger.LogInfo(nameof(WorkbookServiceResolver), "connect_reused", new Dictionary<string, object?>
            {
                ["workbookPath"] = reuseForPath,
                ["connectionId"] = existing!.ConnectionId,
                ["connectionMode"] = existing.ConnectionMode
            });
            return existing!.ToConnectResult(reusedExistingConnection: true, GetApprovalStatus(existing));
        }

        var session = ExcelApplicationSession.AttachToRunning(SessionAttachTarget.ForWorkbook(reuseForPath), _logger);
        try
        {
            var service = new WorkbookService(session, new WorkbookOperationSafety(session, _approvalRegistry, _logger), _logger);
            var diagnostics = await session.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
            var connection = new ConnectedWorkbookConnection(
                ConnectionId: Guid.NewGuid().ToString("N"),
                WorkbookName: workbook.Name,
                WorkbookPath: reuseForPath,
                ConnectionMode: "attached",
                SessionMode: diagnostics.SessionMode == ExcelSessionMode.AttachToRunning ? "attach" : "create-new",
                AttachTarget: DiagnosticsAttachTargetToString(diagnostics.AttachTargetMode),
                IsOpenInExcel: true,
                Session: session,
                Service: service,
                OwnsSession: true);

            RegisterConnection(connection);
            _logger.LogInfo(nameof(WorkbookServiceResolver), "connect_attached", new Dictionary<string, object?>
            {
                ["connectionId"] = connection.ConnectionId,
                ["workbookPath"] = reuseForPath,
                ["attachTarget"] = connection.AttachTarget
            });
            return connection.ToConnectResult(reusedExistingConnection: false, GetApprovalStatus(connection));
        }
        catch (Exception ex)
        {
            _logger.LogInfo(nameof(WorkbookServiceResolver), "connect_attached_failed", new Dictionary<string, object?>
            {
                ["workbookPath"] = reuseForPath
            }, ex);
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<WorkbookConnectionResult> ConnectBridgeOwnedAsync(
        string workbookPath,
        string workbookName,
        CancellationToken cancellationToken)
    {
        if (TryGetExistingConnection(workbookPath, out var existing))
        {
            _logger.LogInfo(nameof(WorkbookServiceResolver), "connect_reused", new Dictionary<string, object?>
            {
                ["workbookPath"] = workbookPath,
                ["connectionId"] = existing!.ConnectionId,
                ["connectionMode"] = existing.ConnectionMode
            });
            return existing!.ToConnectResult(reusedExistingConnection: true, GetApprovalStatus(existing));
        }

        var (session, service) = GetOrCreateBridgeOwnedService();
        await using var workbook = await session.OpenWorkbookAsync(workbookPath, cancellationToken).ConfigureAwait(false);

        var diagnostics = await session.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        var connection = new ConnectedWorkbookConnection(
            ConnectionId: Guid.NewGuid().ToString("N"),
            WorkbookName: workbookName,
            WorkbookPath: workbookPath,
            ConnectionMode: "bridge_owned",
            SessionMode: diagnostics.SessionMode == ExcelSessionMode.AttachToRunning ? "attach" : "create-new",
            AttachTarget: DiagnosticsAttachTargetToString(diagnostics.AttachTargetMode),
            IsOpenInExcel: false,
            Session: session,
            Service: service,
            OwnsSession: false);

        RegisterConnection(connection);
        _logger.LogInfo(nameof(WorkbookServiceResolver), "connect_bridge_owned", new Dictionary<string, object?>
        {
            ["connectionId"] = connection.ConnectionId,
            ["workbookPath"] = workbookPath
        });
        return connection.ToConnectResult(reusedExistingConnection: false, GetApprovalStatus(connection));
    }

    private void RegisterConnection(ConnectedWorkbookConnection connection)
    {
        lock (_gate)
        {
            _connectionsById[connection.ConnectionId] = connection;
            _connectionIdsByPath[connection.WorkbookPath] = connection.ConnectionId;
        }
    }

    private bool TryGetExistingConnection(string workbookPath, out ConnectedWorkbookConnection? connection)
    {
        lock (_gate)
        {
            if (_connectionIdsByPath.TryGetValue(workbookPath, out var connectionId) &&
                _connectionsById.TryGetValue(connectionId, out connection))
            {
                return true;
            }
        }

        connection = null;
        return false;
    }

    private async Task<ResolvedWorkbookContext> ResolveTargetAsync(WorkbookTarget target, CancellationToken cancellationToken)
    {
        var explicitPath = NormalizePathOrNull(target.WorkbookPath);
        var connectionId = NormalizeIdentifier(target.ConnectionId);

        if (explicitPath is null && connectionId is null)
        {
            throw new WorkbookTargetResolutionException(
                "workbook_target_required",
                "This tool requires either 'workbookPath' or 'connectionId'.");
        }

        if (connectionId is not null)
        {
            ConnectedWorkbookConnection connection;
            lock (_gate)
            {
                if (!_connectionsById.TryGetValue(connectionId, out connection!))
                {
                    throw new WorkbookTargetResolutionException(
                        "connection_not_found",
                        $"No workbook connection with id '{connectionId}' exists.");
                }
            }

            if (explicitPath is not null &&
                !string.Equals(explicitPath, connection.WorkbookPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkbookTargetResolutionException(
                    "workbook_target_mismatch",
                    "The provided 'workbookPath' does not match the workbook selected by 'connectionId'.",
                    $"connectionId '{connectionId}' resolves to '{connection.WorkbookPath}'.");
            }

            _logger.LogDebug(nameof(WorkbookServiceResolver), "resolve_target_connection", new Dictionary<string, object?>
            {
                ["connectionId"] = connectionId,
                ["workbookPath"] = connection.WorkbookPath
            });
            return new ResolvedWorkbookContext(connection.WorkbookPath, connection.ConnectionId, connection.Service);
        }

        if (_options.SessionMode == SessionMode.Attach &&
            _options.AttachTarget == SessionAttachTargetMode.WorkbookOwner)
        {
            _logger.LogDebug(nameof(WorkbookServiceResolver), "resolve_target_borrowed_attach", new Dictionary<string, object?>
            {
                ["workbookPath"] = explicitPath
            });
            var session = ExcelApplicationSession.AttachToRunning(SessionAttachTarget.ForWorkbook(explicitPath!), _logger);
            var service = new WorkbookService(session, new WorkbookOperationSafety(session, _approvalRegistry, _logger), _logger);
            return new BorrowedResolvedWorkbookContext(explicitPath!, null, service, session);
        }

        var (_, sharedService) = GetOrCreateDefaultSharedService();
        _logger.LogDebug(nameof(WorkbookServiceResolver), "resolve_target_shared", new Dictionary<string, object?>
        {
            ["workbookPath"] = explicitPath
        });
        return new ResolvedWorkbookContext(explicitPath!, null, sharedService);
    }

    private (IExcelSession Session, WorkbookService Service) GetOrCreateDefaultSharedService()
    {
        lock (_gate)
        {
            if (_defaultSharedSession is not null && _defaultSharedService is not null)
            {
                return (_defaultSharedSession, _defaultSharedService);
            }

            _defaultSharedSession = CreateDefaultSharedSession();
            _defaultSharedService = new WorkbookService(_defaultSharedSession, new WorkbookOperationSafety(_defaultSharedSession, _approvalRegistry, _logger), _logger);
            _logger.LogInfo(nameof(WorkbookServiceResolver), "shared_service_created", new Dictionary<string, object?>
            {
                ["sessionMode"] = _options.SessionMode.ToString(),
                ["attachTarget"] = _options.AttachTarget.ToString()
            });
            return (_defaultSharedSession, _defaultSharedService);
        }
    }

    private (IExcelSession Session, WorkbookService Service) GetOrCreateBridgeOwnedService()
    {
        lock (_gate)
        {
            if (_bridgeOwnedSession is not null && _bridgeOwnedService is not null)
            {
                return (_bridgeOwnedSession, _bridgeOwnedService);
            }

            _bridgeOwnedSession = ExcelApplicationSession.CreateNew(_options.Visible, _logger);
            _bridgeOwnedService = new WorkbookService(_bridgeOwnedSession, new WorkbookOperationSafety(_bridgeOwnedSession, _approvalRegistry, _logger), _logger);
            _logger.LogInfo(nameof(WorkbookServiceResolver), "bridge_owned_service_created", new Dictionary<string, object?>
            {
                ["visible"] = _options.Visible
            });
            return (_bridgeOwnedSession, _bridgeOwnedService);
        }
    }

    private IExcelSession CreateDefaultSharedSession() =>
        _options.SessionMode switch
        {
            SessionMode.Attach when _options.AttachTarget == SessionAttachTargetMode.AnyRunningInstance =>
                ExcelApplicationSession.AttachToRunning(SessionAttachTarget.AnyRunningInstance, _logger),
            _ => ExcelApplicationSession.CreateNew(_options.Visible, _logger)
        };

    internal static string? NormalizePathOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return RunningWorkbookObjectTable.NormalizePath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static string? NormalizeIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureAttachedWorkbookOwnerConnectionAsync(
        ResolvedWorkbookContext resolved,
        CancellationToken cancellationToken)
    {
        if (resolved.ConnectionId is not null)
        {
            var connection = await GetConnectionAsync(resolved.ConnectionId, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(connection.ConnectionMode, "attached", StringComparison.Ordinal))
            {
                throw new AttachedMutationApprovalModeException(
                    "attached_session_approval_not_applicable",
                    "Attached-session mutation approval is only available for attached workbook connections.",
                    "Connect to an already-open workbook before granting attached mutation approval.");
            }

            if (!string.Equals(connection.AttachTarget, "workbook-owner", StringComparison.Ordinal))
            {
                throw new AttachedMutationApprovalModeException(
                    "attached_session_approval_scope_mismatch",
                    "Attached-session mutation approval requires workbook-owner attach targeting.",
                    "Reconnect the workbook through workbook-owner attachment.");
            }

            return;
        }

        if (_options.SessionMode == SessionMode.Attach &&
            _options.AttachTarget == SessionAttachTargetMode.WorkbookOwner)
        {
            await using var _ = ExcelApplicationSession.AttachToRunning(SessionAttachTarget.ForWorkbook(resolved.WorkbookPath), _logger);
            return;
        }

        throw new AttachedMutationApprovalModeException(
            "attached_session_approval_not_applicable",
            "Attached-session mutation approval is only available for attached workbook targets.",
            "Connect to an already-open workbook or run the operation directly against a workbook-owner attached session.");
    }

    private static string? DiagnosticsAttachTargetToString(SessionAttachTargetMode? target) =>
        target switch
        {
            SessionAttachTargetMode.AnyRunningInstance => "any-running",
            SessionAttachTargetMode.WorkbookOwner => "workbook-owner",
            _ => null
        };

    private WorkbookApprovalStatus GetApprovalStatus(ConnectedWorkbookConnection connection)
    {
        if (!string.Equals(connection.ConnectionMode, "attached", StringComparison.Ordinal) ||
            !string.Equals(connection.AttachTarget, "workbook-owner", StringComparison.Ordinal))
        {
            return new WorkbookApprovalStatus("not_applicable", null, null);
        }

        var lookup = _approvalRegistry.Lookup(connection.WorkbookPath);
        return lookup.State switch
        {
            AttachedMutationApprovalState.Active => new WorkbookApprovalStatus(
                "active",
                lookup.Lease?.ExpiresAtUtc,
                lookup.Lease?.LastUsedAtUtc),
            AttachedMutationApprovalState.Expired => new WorkbookApprovalStatus(
                "expired",
                lookup.Lease?.ExpiresAtUtc,
                lookup.Lease?.LastUsedAtUtc),
            _ => new WorkbookApprovalStatus("missing", null, null)
        };
    }

    private sealed record WorkbookApprovalStatus(
        string State,
        DateTimeOffset? ExpiresAtUtc,
        DateTimeOffset? LastUsedAtUtc);

    private sealed record ConnectedWorkbookConnection(
        string ConnectionId,
        string WorkbookName,
        string WorkbookPath,
        string ConnectionMode,
        string SessionMode,
        string? AttachTarget,
        bool IsOpenInExcel,
        IExcelSession Session,
        WorkbookService Service,
        bool OwnsSession)
    {
        public WorkbookConnectionInfo ToInfo(WorkbookApprovalStatus approvalStatus) =>
            new(
                ConnectionId,
                WorkbookName,
                WorkbookPath,
                ConnectionMode,
                SessionMode,
                AttachTarget,
                IsOpenInExcel,
                approvalStatus.State,
                approvalStatus.ExpiresAtUtc,
                approvalStatus.LastUsedAtUtc);

        public WorkbookConnectionResult ToConnectResult(bool reusedExistingConnection, WorkbookApprovalStatus approvalStatus) =>
            new(
                true,
                ConnectionId,
                WorkbookName,
                WorkbookPath,
                ConnectionMode,
                SessionMode,
                AttachTarget,
                reusedExistingConnection,
                IsOpenInExcel,
                approvalStatus.State,
                approvalStatus.ExpiresAtUtc,
                approvalStatus.LastUsedAtUtc);
    }

    private sealed record BorrowedResolvedWorkbookContext : ResolvedWorkbookContext, IAsyncDisposable
    {
        private readonly IExcelSession _session;

        public BorrowedResolvedWorkbookContext(string workbookPath, string? connectionId, WorkbookService service, IExcelSession session)
            : base(workbookPath, connectionId, service)
        {
            _session = session;
        }

        public ValueTask DisposeAsync() => _session.DisposeAsync();
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
