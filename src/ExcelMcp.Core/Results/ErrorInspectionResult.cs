namespace ExcelMcp.Core.Results;

public sealed record ErrorInspectionResult(
    bool Succeeded,
    string WorkbookPath,
    string Scope,
    string? SheetName,
    string? Address,
    int HitCount,
    IReadOnlyList<ExcelMcp.Core.ErrorInspectionHit> Hits,
    OperationError? Error = null);
