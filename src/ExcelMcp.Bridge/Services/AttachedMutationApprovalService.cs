using ExcelMcp.Core;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class AttachedMutationApprovalService
{
    public static readonly TimeSpan DefaultApprovalTtl = TimeSpan.FromMinutes(10);

    private readonly IAttachedMutationApprovalRegistry _registry;

    public AttachedMutationApprovalService(IAttachedMutationApprovalRegistry registry)
    {
        _registry = registry;
    }

    public Task<AttachedMutationApprovalGrantResult> GrantAsync(
        string workbookPath,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lease = _registry.Grant(workbookPath, ttl ?? DefaultApprovalTtl, out var refreshed);
        return Task.FromResult(new AttachedMutationApprovalGrantResult(
            Succeeded: true,
            WorkbookPath: lease.WorkbookPath,
            GrantedAtUtc: lease.GrantedAtUtc,
            ExpiresAtUtc: lease.ExpiresAtUtc,
            RefreshedExistingLease: refreshed,
            LastUsedAtUtc: lease.LastUsedAtUtc));
    }

    public Task<AttachedMutationApprovalRevokeResult> RevokeAsync(
        string workbookPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var revoked = _registry.Revoke(workbookPath);
        return Task.FromResult(new AttachedMutationApprovalRevokeResult(
            Succeeded: true,
            WorkbookPath: NormalizePath(workbookPath),
            LeaseExisted: revoked));
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }
}
