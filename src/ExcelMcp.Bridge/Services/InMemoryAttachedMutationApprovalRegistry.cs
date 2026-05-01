using ExcelMcp.Core;

namespace ExcelMcp.Bridge.Services;

public sealed class InMemoryAttachedMutationApprovalRegistry : IAttachedMutationApprovalRegistry
{
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _gate = new();
    private readonly Dictionary<string, AttachedMutationApprovalLease> _leases = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryAttachedMutationApprovalRegistry(Func<DateTimeOffset>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public AttachedMutationApprovalLease Grant(string workbookPath, TimeSpan ttl, out bool refreshedExistingLease)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Approval TTL must be greater than zero.");
        }

        var normalizedPath = NormalizePath(workbookPath);
        var now = _utcNow();

        lock (_gate)
        {
            PruneExpiredLeasesUnsafe(now);
            refreshedExistingLease = _leases.ContainsKey(normalizedPath);
            var lease = new AttachedMutationApprovalLease(
                normalizedPath,
                GrantedAtUtc: now,
                ExpiresAtUtc: now.Add(ttl),
                LastUsedAtUtc: null);
            _leases[normalizedPath] = lease;
            return lease;
        }
    }

    public bool Revoke(string workbookPath)
    {
        var normalizedPath = NormalizePath(workbookPath);
        lock (_gate)
        {
            PruneExpiredLeasesUnsafe(_utcNow());
            return _leases.Remove(normalizedPath);
        }
    }

    public AttachedMutationApprovalLookup Lookup(string workbookPath)
    {
        var normalizedPath = NormalizePath(workbookPath);
        var now = _utcNow();
        lock (_gate)
        {
            if (_leases.TryGetValue(normalizedPath, out var matchingLease))
            {
                if (matchingLease.ExpiresAtUtc <= now)
                {
                    _leases.Remove(normalizedPath);
                    return new AttachedMutationApprovalLookup(AttachedMutationApprovalState.Expired, matchingLease);
                }

                return new AttachedMutationApprovalLookup(AttachedMutationApprovalState.Active, matchingLease);
            }

            PruneExpiredLeasesUnsafe(now);
            if (_leases.Count > 0)
            {
                return new AttachedMutationApprovalLookup(AttachedMutationApprovalState.ScopeMismatch);
            }

            return new AttachedMutationApprovalLookup(AttachedMutationApprovalState.Missing);
        }
    }

    public AttachedMutationApprovalLease Touch(string workbookPath)
    {
        var normalizedPath = NormalizePath(workbookPath);
        var now = _utcNow();
        lock (_gate)
        {
            if (!_leases.TryGetValue(normalizedPath, out var lease))
            {
                throw new InvalidOperationException($"No active approval lease exists for workbook '{normalizedPath}'.");
            }

            if (lease.ExpiresAtUtc <= now)
            {
                _leases.Remove(normalizedPath);
                throw new InvalidOperationException($"Approval lease for workbook '{normalizedPath}' has already expired.");
            }

            var touched = lease with { LastUsedAtUtc = now };
            _leases[normalizedPath] = touched;
            return touched;
        }
    }

    private void PruneExpiredLeasesUnsafe(DateTimeOffset now)
    {
        foreach (var expiredKey in _leases
                     .Where(entry => entry.Value.ExpiresAtUtc <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _leases.Remove(expiredKey);
        }
    }

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
