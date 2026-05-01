using ExcelMcp.Core;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.Core.Logging;
using ExcelMcp.Core.Results;

namespace ExcelMcp.Bridge.Services;

public sealed class WorkbookOperationSafety
{
    private readonly IExcelSession _session;
    private readonly IMutationPermissionRegistry? _permissionRegistry;
    private readonly IGridPilotLogger _logger;

    public WorkbookOperationSafety(IExcelSession session, IMutationPermissionRegistry? permissionRegistry = null, IGridPilotLogger? logger = null)
    {
        _session = session;
        _permissionRegistry = permissionRegistry;
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
        var isAttachedSession = diagnostics.SessionMode == ExcelSessionMode.AttachToRunning;
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
        if (isAttachedSession)
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

        if (isAttachedSession)
        {
            if (_permissionRegistry is null)
            {
                return LogAndReturn(
                    Code: "shared_session_approval_required",
                    Message: $"Operation '{GetOperationLabel(intent)}' requires attached-session mutation approval.",
                    Detail: "Grant workbook-scoped or session-scoped mutation permission before running mutating tools against a live attached workbook.",
                    Source: nameof(WorkbookOperationSafety),
                    workbookPath: normalizedTarget,
                    intent: intent);
            }
        }

        if (_permissionRegistry is null)
        {
            _logger.LogDebug(nameof(WorkbookOperationSafety), "check_allowed_without_permission_registry", new Dictionary<string, object?>
            {
                ["workbookPath"] = normalizedTarget,
                ["intent"] = GetOperationLabel(intent)
            });
            return null;
        }

        var permission = _permissionRegistry.Lookup(normalizedTarget);
        switch (permission.State)
        {
            case MutationPermissionState.Active:
                if (permission.Scope == MutationPermissionScope.Session)
                {
                    _permissionRegistry.TouchSession();
                }
                else
                {
                    _permissionRegistry.TouchWorkbook(normalizedTarget);
                }

                _logger.LogDebug(nameof(WorkbookOperationSafety), "permission_active", new Dictionary<string, object?>
                {
                    ["workbookPath"] = normalizedTarget,
                    ["intent"] = GetOperationLabel(intent),
                    ["scope"] = permission.Scope.ToString()
                });
                return null;
            case MutationPermissionState.Expired:
                return LogAndReturn(
                    Code: isAttachedSession ? "shared_session_approval_expired" : "mutation_permission_expired",
                    Message: isAttachedSession
                        ? $"Operation '{GetOperationLabel(intent)}' is blocked because the attached-session mutation approval has expired."
                        : $"Operation '{GetOperationLabel(intent)}' is blocked because the mutation permission has expired.",
                    Detail: permission.Scope == MutationPermissionScope.Session
                        ? $"The session-wide mutation permission expired at {permission.Lease!.ExpiresAtUtc:O}."
                        : $"The workbook-scoped mutation permission for '{normalizedTarget}' expired at {permission.Lease!.ExpiresAtUtc:O}.",
                    Source: nameof(WorkbookOperationSafety),
                    workbookPath: normalizedTarget,
                    intent: intent);
            case MutationPermissionState.ScopeMismatch:
                return LogAndReturn(
                    Code: isAttachedSession ? "shared_session_approval_scope_mismatch" : "mutation_permission_scope_mismatch",
                    Message: $"Operation '{GetOperationLabel(intent)}' is blocked because permission exists for a different workbook.",
                    Detail: "Grant mutation permission for the exact workbook path being targeted, or grant session-scoped mutation permission.",
                    Source: nameof(WorkbookOperationSafety),
                    workbookPath: normalizedTarget,
                    intent: intent);
            default:
                return LogAndReturn(
                    Code: isAttachedSession ? "shared_session_approval_required" : "mutation_permission_required",
                    Message: isAttachedSession
                        ? $"Operation '{GetOperationLabel(intent)}' requires attached-session mutation approval."
                        : $"Operation '{GetOperationLabel(intent)}' requires mutation permission.",
                    Detail: isAttachedSession
                        ? "Grant workbook-scoped or session-scoped mutation permission before running mutating tools against a live attached workbook."
                        : "Grant workbook-scoped or session-scoped mutation permission before running mutating tools.",
                    Source: nameof(WorkbookOperationSafety),
                    workbookPath: normalizedTarget,
                    intent: intent);
        }
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
