namespace ExcelMcp.Core;

public sealed record MutationPermissionLease(
    MutationPermissionScope Scope,
    string? WorkbookPath,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc = null);
