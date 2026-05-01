using ExcelMcp.Bridge.Services;
using ExcelMcp.Core;

namespace ExcelMcp.ToolHost;

internal sealed record WorkbookTarget(string? WorkbookPath, string? ConnectionId);

internal record ResolvedWorkbookContext(
    string WorkbookPath,
    string? ConnectionId,
    WorkbookService Service);

internal sealed record WorkbookConnectionInfo(
    string ConnectionId,
    string WorkbookName,
    string WorkbookPath,
    string ConnectionMode,
    string SessionMode,
    string? AttachTarget,
    bool IsOpenInExcel,
    string ApprovalState,
    DateTimeOffset? ApprovalExpiresAtUtc,
    DateTimeOffset? ApprovalLastUsedAtUtc)
{
    public string HostSessionId { get; init; } = string.Empty;
    public string MutationPermissionState { get; init; } = "missing";
    public string MutationPermissionScope { get; init; } = "none";
    public string? MutationPermissionWorkbookPath { get; init; }
    public DateTimeOffset? MutationPermissionExpiresAtUtc { get; init; }
    public DateTimeOffset? MutationPermissionLastUsedAtUtc { get; init; }
}

internal sealed record WorkbookConnectionRequest(string? WorkbookPath, string? WorkbookName);

internal sealed record WorkbookCreateRequest(string WorkbookPath);

internal sealed record WorkbookSaveAsRequest(
    string? WorkbookPath,
    string? ConnectionId,
    string NewWorkbookPath);

internal sealed record WorkbookConnectionResult(
    bool Succeeded,
    string ConnectionId,
    string WorkbookName,
    string WorkbookPath,
    string ConnectionMode,
    string SessionMode,
    string? AttachTarget,
    bool ReusedExistingConnection,
    bool IsOpenInExcel,
    string ApprovalState,
    DateTimeOffset? ApprovalExpiresAtUtc,
    DateTimeOffset? ApprovalLastUsedAtUtc)
{
    public string HostSessionId { get; init; } = string.Empty;
    public string MutationPermissionState { get; init; } = "missing";
    public string MutationPermissionScope { get; init; } = "none";
    public string? MutationPermissionWorkbookPath { get; init; }
    public DateTimeOffset? MutationPermissionExpiresAtUtc { get; init; }
    public DateTimeOffset? MutationPermissionLastUsedAtUtc { get; init; }
}

internal sealed record MutationPermissionGrantRequest(
    string Scope,
    string? WorkbookPath,
    string? ConnectionId);

internal sealed record MutationPermissionRevokeRequest(
    string Scope,
    string? WorkbookPath,
    string? ConnectionId);

internal sealed record MutationPermissionStatusRequest(
    string Scope,
    string? WorkbookPath,
    string? ConnectionId);

internal sealed record WorkbookDisconnectResult(
    bool Succeeded,
    string ConnectionId,
    string WorkbookPath,
    bool ConnectionExisted);

internal sealed class WorkbookTargetResolutionException : Exception
{
    public WorkbookTargetResolutionException(string code, string message, string? detail = null)
        : base(message)
    {
        Code = code;
        Detail = detail;
    }

    public string Code { get; }

    public string? Detail { get; }
}
