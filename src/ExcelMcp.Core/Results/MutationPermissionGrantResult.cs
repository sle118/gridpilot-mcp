namespace ExcelMcp.Core.Results;

public sealed record MutationPermissionGrantResult(
    bool Succeeded,
    string HostSessionId,
    string Scope,
    string? WorkbookPath,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool RefreshedExistingLease,
    DateTimeOffset? LastUsedAtUtc = null,
    OperationError? Error = null);
