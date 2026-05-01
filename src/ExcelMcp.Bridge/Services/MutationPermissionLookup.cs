using ExcelMcp.Core;

namespace ExcelMcp.Bridge.Services;

public enum MutationPermissionState
{
    Missing = 0,
    ScopeMismatch = 1,
    Expired = 2,
    Active = 3
}

public sealed record MutationPermissionLookup(
    MutationPermissionState State,
    MutationPermissionScope Scope,
    MutationPermissionLease? Lease = null);
