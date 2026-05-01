using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Logging;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class WorkbookOperationSafety
{
    private readonly IExcelSession _session;
    private readonly IAttachedMutationApprovalRegistry? _approvalRegistry;
    private readonly IGridPilotLogger _logger;

    public WorkbookOperationSafety(IExcelSession session, IAttachedMutationApprovalRegistry? approvalRegistry = null, IGridPilotLogger? logger = null)
    {
        _session = session;
        _approvalRegistry = approvalRegistry;
        _logger = logger ?? GridPilotNullLogger.Instance;
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
        _logger.LogTrace(nameof(WorkbookOperationSafety), "check_started", new Dictionary<string, object?>
        {
            ["workbookPath"] = normalizedTarget,
            ["intent"] = GetOperationLabel(intent),
            ["sessionMode"] = diagnostics.SessionMode.ToString(),
            ["attachTarget"] = diagnostics.AttachTargetMode?.ToString(),
            ["isReady"] = diagnostics.IsReady,
            ["isInteractive"] = diagnostics.IsInteractive,
            ["calculationState"] = diagnostics.CalculationState.ToString()
        });
        if (diagnostics.SessionMode == ExcelSessionMode.AttachToRunning)
        {
            if (diagnostics.AttachTargetMode is not SessionAttachTargetMode.WorkbookOwner)
            {
                return LogAndReturn(
                    Code: "shared_session_approval_scope_mismatch",
                    Message: $"Operation '{GetOperationLabel(intent)}' requires a workbook-owner attached session.",
                    Detail: "Attached mutation approval only applies when the bridge is attached to the running Excel instance that already owns the requested workbook.",
                    Source: nameof(WorkbookOperationSafety),
                    workbookPath: normalizedTarget,
                    intent: intent);
            }
        }

        if (diagnostics.IsEditingCell)
        {
            return LogAndReturn(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel appears to be in active cell edit mode.",
                Detail: "Excel reported a non-ready but still interactive state, which is treated as in-progress cell editing for attached-session safety.",
                Source: nameof(WorkbookOperationSafety),
                workbookPath: normalizedTarget,
                intent: intent);
        }

        if (diagnostics.HasModalUi)
        {
            return LogAndReturn(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel appears to have modal UI open.",
                Detail: "Excel reported a non-interactive state, which is treated as modal or automation-blocking UI.",
                Source: nameof(WorkbookOperationSafety),
                workbookPath: normalizedTarget,
                intent: intent);
        }

        if (!diagnostics.IsReady)
        {
            return LogAndReturn(
                Code: "shared_session_ui_unsafe",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel is not ready for safe mutation.",
                Detail: "Excel reported a non-ready UI state, which can indicate an in-progress edit, modal dialog, or another transient application interaction.",
                Source: nameof(WorkbookOperationSafety),
                workbookPath: normalizedTarget,
                intent: intent);
        }

        if (diagnostics.IsBusy || diagnostics.CalculationState is ExcelCalculationState.Calculating or ExcelCalculationState.Pending)
        {
            return LogAndReturn(
                Code: "shared_session_busy",
                Message: $"Operation '{GetOperationLabel(intent)}' is blocked because Excel is in a transient calculation or refresh state.",
                Detail: $"Excel calculation state is '{diagnostics.CalculationState}'.",
                Source: nameof(WorkbookOperationSafety),
                workbookPath: normalizedTarget,
                intent: intent);
        }

        if (diagnostics.SessionMode == ExcelSessionMode.AttachToRunning)
        {
            if (_approvalRegistry is null)
            {
                return LogAndReturn(
                    Code: "shared_session_approval_required",
                    Message: $"Operation '{GetOperationLabel(intent)}' requires attached-session mutation approval.",
                    Detail: "Grant a workbook-scoped attached-session mutation approval lease before running mutating tools against a live attached workbook.",
                    Source: nameof(WorkbookOperationSafety),
                    workbookPath: normalizedTarget,
                    intent: intent);
            }

            var approval = _approvalRegistry.Lookup(normalizedTarget);
            switch (approval.State)
            {
                case AttachedMutationApprovalState.Active:
                    _approvalRegistry.Touch(normalizedTarget);
                    _logger.LogDebug(nameof(WorkbookOperationSafety), "approval_active", new Dictionary<string, object?>
                    {
                        ["workbookPath"] = normalizedTarget,
                        ["intent"] = GetOperationLabel(intent)
                    });
                    return null;
                case AttachedMutationApprovalState.Expired:
                    return LogAndReturn(
                        Code: "shared_session_approval_expired",
                        Message: $"Operation '{GetOperationLabel(intent)}' is blocked because the attached-session mutation approval has expired.",
                        Detail: $"The workbook-scoped approval lease for '{normalizedTarget}' expired at {approval.Lease!.ExpiresAtUtc:O}.",
                        Source: nameof(WorkbookOperationSafety),
                        workbookPath: normalizedTarget,
                        intent: intent);
                case AttachedMutationApprovalState.ScopeMismatch:
                    return LogAndReturn(
                        Code: "shared_session_approval_scope_mismatch",
                        Message: $"Operation '{GetOperationLabel(intent)}' is blocked because approval exists for a different workbook.",
                        Detail: "Grant mutation approval for the exact workbook path being targeted by this operation.",
                        Source: nameof(WorkbookOperationSafety),
                        workbookPath: normalizedTarget,
                        intent: intent);
                default:
                    return LogAndReturn(
                        Code: "shared_session_approval_required",
                        Message: $"Operation '{GetOperationLabel(intent)}' requires attached-session mutation approval.",
                        Detail: "Grant a workbook-scoped attached-session mutation approval lease before running mutating tools against a live attached workbook.",
                        Source: nameof(WorkbookOperationSafety),
                        workbookPath: normalizedTarget,
                        intent: intent);
            }
        }

        _logger.LogDebug(nameof(WorkbookOperationSafety), "check_allowed", new Dictionary<string, object?>
        {
            ["workbookPath"] = normalizedTarget,
            ["intent"] = GetOperationLabel(intent)
        });
        return null;
    }

    private OperationError LogAndReturn(
        string Code,
        string Message,
        string Detail,
        string Source,
        string workbookPath,
        WorkbookOperationIntent intent)
    {
        var error = new OperationError(Code, Message, Detail, Source);
        _logger.LogInfo(nameof(WorkbookOperationSafety), "check_blocked", new Dictionary<string, object?>
        {
            ["workbookPath"] = workbookPath,
            ["intent"] = GetOperationLabel(intent),
            ["code"] = Code
        });
        return error;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return WorkbookIdentity.Normalize(path);
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
