namespace ExcelMcp.Core.Results;

public sealed record WorkbookSaveResult(
    bool Succeeded,
    string SourceWorkbookPath,
    string WorkbookPath,
    string Operation,
    string? ConnectionId = null,
    OperationError? Error = null)
{
    public string ApprovalState { get; init; } = "missing";
    public DateTimeOffset? ApprovalExpiresAtUtc { get; init; }
    public DateTimeOffset? ApprovalLastUsedAtUtc { get; init; }
    public string HostSessionId { get; init; } = string.Empty;
    public string MutationPermissionState { get; init; } = "missing";
    public string MutationPermissionScope { get; init; } = "none";
    public string? MutationPermissionWorkbookPath { get; init; }
    public DateTimeOffset? MutationPermissionExpiresAtUtc { get; init; }
    public DateTimeOffset? MutationPermissionLastUsedAtUtc { get; init; }
}
