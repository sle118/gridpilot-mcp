using ExcelMcp.Bridge.Services;
using ExcelMcp.Core;
using ExcelMcp.Core.Results;

namespace ExcelMcp.ToolHost;

internal interface IWorkbookServiceResolver
{
    Task<T> ExecuteAsync<T>(
        WorkbookTarget target,
        Func<ResolvedWorkbookContext, Task<T>> action,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default);

    Task<WorkbookConnectionResult> ConnectAsync(
        WorkbookConnectionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkbookConnectionResult> CreateWorkbookAsync(
        WorkbookCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkbookSaveResult> SaveWorkbookAsync(
        WorkbookTarget target,
        CancellationToken cancellationToken = default);

    Task<WorkbookSaveResult> SaveWorkbookAsAsync(
        WorkbookSaveAsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default);

    Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default);

    Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default);

    Task<SessionDiagnostics> GetSessionDiagnosticsAsync(
        WorkbookTarget target,
        CancellationToken cancellationToken = default) =>
        Task.FromException<SessionDiagnostics>(new WorkbookTargetResolutionException(
            "runtime_diagnostics_not_available",
            "Session diagnostics are not available on this workbook service resolver."));

    Task<MutationPermissionGrantResult> GrantMutationPermissionAsync(
        MutationPermissionGrantRequest request,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    Task<MutationPermissionRevokeResult> RevokeMutationPermissionAsync(
        MutationPermissionRevokeRequest request,
        CancellationToken cancellationToken = default);

    Task<MutationPermissionStatusResult> GetMutationPermissionStatusAsync(
        MutationPermissionStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(
        WorkbookTarget target,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(
        WorkbookTarget target,
        CancellationToken cancellationToken = default);
}
