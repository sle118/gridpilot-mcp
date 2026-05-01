namespace ExcelMcp.Core.Results;

public sealed record MutationPermissionRevokeResult(
    bool Succeeded,
    string HostSessionId,
    string Scope,
    string? WorkbookPath,
    bool LeaseExisted,
    OperationError? Error = null);
