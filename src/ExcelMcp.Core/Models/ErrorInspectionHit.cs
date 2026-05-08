namespace ExcelMcp.Core;

public sealed record ErrorInspectionHit(
    string SheetName,
    string Address,
    bool HasFormula,
    string? Formula,
    string? ErrorCode,
    string ValueKind);
