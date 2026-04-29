namespace ExcelMcp.Core.Results;

public sealed record AttachedMutationApprovalRevokeResult(
    bool Succeeded,
    string WorkbookPath,
    bool LeaseExisted,
    OperationError? Error = null);
