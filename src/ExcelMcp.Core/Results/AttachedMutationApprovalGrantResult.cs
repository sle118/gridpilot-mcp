namespace ExcelMcp.Core.Results;

public sealed record AttachedMutationApprovalGrantResult(
    bool Succeeded,
    string WorkbookPath,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool RefreshedExistingLease,
    DateTimeOffset? LastUsedAtUtc = null,
    OperationError? Error = null);
