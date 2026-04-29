using ExcelMcp.Core;

namespace ExcelMcp.Bridge.Services;

public enum AttachedMutationApprovalState
{
    Missing = 0,
    ScopeMismatch = 1,
    Expired = 2,
    Active = 3
}

public sealed record AttachedMutationApprovalLookup(
    AttachedMutationApprovalState State,
    AttachedMutationApprovalLease? Lease = null);
