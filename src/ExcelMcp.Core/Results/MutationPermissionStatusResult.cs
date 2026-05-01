namespace ExcelMcp.Core.Results;

public sealed record MutationPermissionStatusResult(
    bool Succeeded,
    string HostSessionId,
    string State,
    string Scope,
    string? WorkbookPath,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc,
    OperationError? Error = null);
