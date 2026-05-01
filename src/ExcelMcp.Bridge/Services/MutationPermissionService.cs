using ExcelMcp.Core;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class MutationPermissionService
{
    public static readonly TimeSpan DefaultPermissionTtl = TimeSpan.FromMinutes(10);

    private readonly IMutationPermissionRegistry _registry;
    private readonly string _hostSessionId;

    public MutationPermissionService(IMutationPermissionRegistry registry, string hostSessionId)
    {
        _registry = registry;
        _hostSessionId = hostSessionId;
    }

    public Task<MutationPermissionGrantResult> GrantWorkbookAsync(string workbookPath, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lease = _registry.GrantWorkbook(workbookPath, ttl ?? DefaultPermissionTtl, out var refreshed);
        return Task.FromResult(new MutationPermissionGrantResult(true, _hostSessionId, "workbook", lease.WorkbookPath, lease.GrantedAtUtc, lease.ExpiresAtUtc, refreshed, lease.LastUsedAtUtc));
    }

    public Task<MutationPermissionGrantResult> GrantSessionAsync(TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lease = _registry.GrantSession(ttl ?? DefaultPermissionTtl, out var refreshed);
        return Task.FromResult(new MutationPermissionGrantResult(true, _hostSessionId, "session", null, lease.GrantedAtUtc, lease.ExpiresAtUtc, refreshed, lease.LastUsedAtUtc));
    }

    public Task<MutationPermissionRevokeResult> RevokeWorkbookAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revoked = _registry.RevokeWorkbook(workbookPath);
        return Task.FromResult(new MutationPermissionRevokeResult(true, _hostSessionId, "workbook", NormalizePath(workbookPath), revoked));
    }

    public Task<MutationPermissionRevokeResult> RevokeSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revoked = _registry.RevokeSession();
        return Task.FromResult(new MutationPermissionRevokeResult(true, _hostSessionId, "session", null, revoked));
    }

    public Task<MutationPermissionStatusResult> GetWorkbookStatusAsync(string workbookPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lookup = _registry.Lookup(workbookPath);
        return Task.FromResult(ToStatusResult(lookup, NormalizePath(workbookPath)));
    }

    public Task<MutationPermissionStatusResult> GetSessionStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lookup = _registry.LookupSession();
        return Task.FromResult(ToStatusResult(lookup, null));
    }

    private MutationPermissionStatusResult ToStatusResult(MutationPermissionLookup lookup, string? workbookPath) =>
        new(
            true,
            _hostSessionId,
            lookup.State switch
            {
                MutationPermissionState.Active => "active",
                MutationPermissionState.Expired => "expired",
                MutationPermissionState.ScopeMismatch => "scope_mismatch",
                _ => "missing"
            },
            lookup.Scope switch
            {
                MutationPermissionScope.Workbook => "workbook",
                MutationPermissionScope.Session => "session",
                _ => "none"
            },
            lookup.Lease?.WorkbookPath ?? workbookPath,
            lookup.Lease?.ExpiresAtUtc,
            lookup.Lease?.LastUsedAtUtc);

    private static string NormalizePath(string path)
    {
        try
        {
            return WorkbookIdentity.Normalize(path);
        }
        catch (Exception)
        {
            return path;
        }
    }
}
