using ExcelMcp.Core;

namespace ExcelMcp.Bridge.Services;

public sealed class InMemoryMutationPermissionRegistry : IMutationPermissionRegistry
{
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly object _gate = new();
    private readonly Dictionary<string, MutationPermissionLease> _workbookLeases = new(StringComparer.OrdinalIgnoreCase);
    private MutationPermissionLease? _sessionLease;

    public InMemoryMutationPermissionRegistry(Func<DateTimeOffset>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public MutationPermissionLease GrantWorkbook(string workbookPath, TimeSpan ttl, out bool refreshedExistingLease)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Permission TTL must be greater than zero.");
        }

        var normalizedPath = NormalizePath(workbookPath);
        var now = _utcNow();

        lock (_gate)
        {
            PruneExpiredUnsafe(now);
            refreshedExistingLease = _workbookLeases.ContainsKey(normalizedPath);
            var lease = new MutationPermissionLease(MutationPermissionScope.Workbook, normalizedPath, now, now.Add(ttl), null);
            _workbookLeases[normalizedPath] = lease;
            return lease;
        }
    }

    public MutationPermissionLease GrantSession(TimeSpan ttl, out bool refreshedExistingLease)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Permission TTL must be greater than zero.");
        }

        var now = _utcNow();
        lock (_gate)
        {
            PruneExpiredUnsafe(now);
            refreshedExistingLease = _sessionLease is not null;
            _sessionLease = new MutationPermissionLease(MutationPermissionScope.Session, null, now, now.Add(ttl), null);
            return _sessionLease;
        }
    }

    public bool RevokeWorkbook(string workbookPath)
    {
        var normalizedPath = NormalizePath(workbookPath);
        lock (_gate)
        {
            PruneExpiredUnsafe(_utcNow());
            return _workbookLeases.Remove(normalizedPath);
        }
    }

    public bool RevokeSession()
    {
        lock (_gate)
        {
            PruneExpiredUnsafe(_utcNow());
            var existed = _sessionLease is not null;
            _sessionLease = null;
            return existed;
        }
    }

    public MutationPermissionLookup Lookup(string workbookPath)
    {
        var normalizedPath = NormalizePath(workbookPath);
        var now = _utcNow();
        lock (_gate)
        {
            if (_sessionLease is not null)
            {
                if (_sessionLease.ExpiresAtUtc <= now)
                {
                    var expired = _sessionLease;
                    _sessionLease = null;
                    return new MutationPermissionLookup(MutationPermissionState.Expired, MutationPermissionScope.Session, expired);
                }

                return new MutationPermissionLookup(MutationPermissionState.Active, MutationPermissionScope.Session, _sessionLease);
            }

            if (_workbookLeases.TryGetValue(normalizedPath, out var matchingLease))
            {
                if (matchingLease.ExpiresAtUtc <= now)
                {
                    _workbookLeases.Remove(normalizedPath);
                    return new MutationPermissionLookup(MutationPermissionState.Expired, MutationPermissionScope.Workbook, matchingLease);
                }

                return new MutationPermissionLookup(MutationPermissionState.Active, MutationPermissionScope.Workbook, matchingLease);
            }

            PruneExpiredUnsafe(now);
            if (_workbookLeases.Count > 0)
            {
                return new MutationPermissionLookup(MutationPermissionState.ScopeMismatch, MutationPermissionScope.Workbook);
            }

            return new MutationPermissionLookup(MutationPermissionState.Missing, MutationPermissionScope.None);
        }
    }

    public MutationPermissionLookup LookupSession()
    {
        var now = _utcNow();
        lock (_gate)
        {
            PruneExpiredUnsafe(now);
            if (_sessionLease is null)
            {
                return new MutationPermissionLookup(MutationPermissionState.Missing, MutationPermissionScope.None);
            }

            return new MutationPermissionLookup(MutationPermissionState.Active, MutationPermissionScope.Session, _sessionLease);
        }
    }

    public MutationPermissionLease TouchWorkbook(string workbookPath)
    {
        var normalizedPath = NormalizePath(workbookPath);
        var now = _utcNow();
        lock (_gate)
        {
            if (!_workbookLeases.TryGetValue(normalizedPath, out var lease))
            {
                throw new InvalidOperationException($"No active workbook permission exists for workbook '{normalizedPath}'.");
            }

            if (lease.ExpiresAtUtc <= now)
            {
                _workbookLeases.Remove(normalizedPath);
                throw new InvalidOperationException($"Workbook permission for '{normalizedPath}' has already expired.");
            }

            var touched = lease with { LastUsedAtUtc = now };
            _workbookLeases[normalizedPath] = touched;
            return touched;
        }
    }

    public MutationPermissionLease TouchSession()
    {
        var now = _utcNow();
        lock (_gate)
        {
            if (_sessionLease is null)
            {
                throw new InvalidOperationException("No active session-wide mutation permission exists.");
            }

            if (_sessionLease.ExpiresAtUtc <= now)
            {
                _sessionLease = null;
                throw new InvalidOperationException("Session-wide mutation permission has already expired.");
            }

            _sessionLease = _sessionLease with { LastUsedAtUtc = now };
            return _sessionLease;
        }
    }

    private void PruneExpiredUnsafe(DateTimeOffset now)
    {
        foreach (var expiredKey in _workbookLeases.Where(entry => entry.Value.ExpiresAtUtc <= now).Select(entry => entry.Key).ToArray())
        {
            _workbookLeases.Remove(expiredKey);
        }

        if (_sessionLease is not null && _sessionLease.ExpiresAtUtc <= now)
        {
            _sessionLease = null;
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
