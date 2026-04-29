using ExcelMcp.Bridge.Services;
using ExcelMcp.Core.Results;

namespace ExcelMcp.ToolHost;

internal interface IWorkbookServiceResolver
{
    Task<T> ExecuteAsync<T>(
        string workbookPath,
        Func<WorkbookService, Task<T>> action,
        CancellationToken cancellationToken = default);

    Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(
        string workbookPath,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(
        string workbookPath,
        CancellationToken cancellationToken = default);
}
