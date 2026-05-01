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
    DateTimeOffset? ApprovalLastUsedAtUtc);

internal sealed record WorkbookConnectionRequest(string? WorkbookPath, string? WorkbookName);

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
    DateTimeOffset? ApprovalLastUsedAtUtc);

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
