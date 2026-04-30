namespace ExcelMcp.Core;

public sealed record SessionDiagnostics(
    ExcelSessionMode SessionMode,
    bool IsReady,
    bool IsInteractive,
    ExcelCalculationState CalculationState,
    SessionAttachTargetMode? AttachTargetMode = null,
    bool IsEditingCell = false,
    bool HasModalUi = false,
    bool IsBusy = false);
