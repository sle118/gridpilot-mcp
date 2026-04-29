namespace ExcelMcp.Core;

public sealed record SessionDiagnostics(
    ExcelSessionMode SessionMode,
    bool IsReady,
    bool IsInteractive,
    ExcelCalculationState CalculationState);
