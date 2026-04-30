using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class WorkbookOperationSafety
{
    private readonly IExcelSession _session;
    private readonly IAttachedMutationApprovalRegistry? _approvalRegistry;

    public WorkbookOperationSafety(IExcelSession session, IAttachedMutationApprovalRegistry? approvalRegistry = null)
    {
        _session = session;
        _approvalRegistry = approvalRegistry;
    }

    public async Task<OperationError?> CheckAsync(
        string workbookPath,
        WorkbookOperationIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (intent == WorkbookOperationIntent.ReadOnly)
        {
            return null;
        }

        var diagnostics = await _session.GetDiagnosticsAsync(cancellationToken).ConfigureAwait(false);
        var normalizedTarget = NormalizePath(workbookPath);
        if (diagnostics.SessionMode == ExcelSessionMode.AttachToRunning)
        {
            if (diagnostics.AttachTargetMode is not SessionAttachTargetMode.WorkbookOwner)
            {
                return new OperationError(
                    Code: "shared_session_approval_scope_mismatch",
                    Message: $"Operation '{GetOperationLabel(intent)}' requires a workbook-owner attached session.",
                    Detail: "Attached mutation approval only applies when the bridge is attached to the running Excel instance that already owns the requested workbook.",
                    Source: nameof(WorkbookOperationSafety));
            }
        }

        if (diagnostics.IsEditingCell)
        {
            return new OperationError(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel appears to be in active cell edit mode.",
                Detail: "Excel reported a non-ready but still interactive state, which is treated as in-progress cell editing for attached-session safety.",
                Source: nameof(WorkbookOperationSafety));
        }

        if (diagnostics.HasModalUi)
        {
            return new OperationError(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel appears to have modal UI open.",
                Detail: "Excel reported a non-interactive state, which is treated as modal or automation-blocking UI.",
                Source: nameof(WorkbookOperationSafety));
        }

        if (!diagnostics.IsReady)
        {
            return new OperationError(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel is not ready for safe mutation.",
                Detail: "Excel reported a non-ready UI state, which can indicate an in-progress edit, modal dialog, or another transient application interaction.",
                Source: nameof(WorkbookOperationSafety));
        }

        if (diagnostics.IsBusy || diagnostics.CalculationState is ExcelCalculationState.Calculating or ExcelCalculationState.Pending)
        {
            return new OperationError(
                Code: "shared_session_busy",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel is in a transient calculation or refresh state.",
                Detail: $"Excel calculation state is '{diagnostics.CalculationState}'.",
                Source: nameof(WorkbookOperationSafety));
        }

        if (diagnostics.SessionMode == ExcelSessionMode.AttachToRunning)
        {
            if (_approvalRegistry is null)
            {
                return new OperationError(
                    Code: "shared_session_approval_required",
                    Message: $"Operation '{GetOperationLabel(intent)}' requires attached-session mutation approval.",
                    Detail: "Grant a workbook-scoped attached-session mutation approval lease before running mutating tools against a live attached workbook.",
                    Source: nameof(WorkbookOperationSafety));
            }

            var approval = _approvalRegistry.Lookup(normalizedTarget);
            switch (approval.State)
            {
                case AttachedMutationApprovalState.Active:
                    _approvalRegistry.Touch(normalizedTarget);
                    return null;
                case AttachedMutationApprovalState.Expired:
                    return new OperationError(
                        Code: "shared_session_approval_expired",
                        Message: $"Operation '{GetOperationLabel(intent)}' is blocked because the attached-session mutation approval has expired.",
                        Detail: $"The workbook-scoped approval lease for '{normalizedTarget}' expired at {approval.Lease!.ExpiresAtUtc:O}.",
                        Source: nameof(WorkbookOperationSafety));
                case AttachedMutationApprovalState.ScopeMismatch:
                    return new OperationError(
                        Code: "shared_session_approval_scope_mismatch",
                        Message: $"Operation '{GetOperationLabel(intent)}' is blocked because approval exists for a different workbook.",
                        Detail: "Grant mutation approval for the exact workbook path being targeted by this operation.",
                        Source: nameof(WorkbookOperationSafety));
                default:
                    return new OperationError(
                        Code: "shared_session_approval_required",
                        Message: $"Operation '{GetOperationLabel(intent)}' requires attached-session mutation approval.",
                        Detail: "Grant a workbook-scoped attached-session mutation approval lease before running mutating tools against a live attached workbook.",
                        Source: nameof(WorkbookOperationSafety));
            }
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static string GetOperationLabel(WorkbookOperationIntent intent) =>
        intent switch
        {
            WorkbookOperationIntent.Mutating => "mutating",
            WorkbookOperationIntent.DiagnosticTempWrite => "diagnostic_temp_write",
            _ => "read_only"
        };
}
