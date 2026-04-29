namespace ExcelMcp.Core;

public sealed record AttachedMutationApprovalLease(
    string WorkbookPath,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc = null);
