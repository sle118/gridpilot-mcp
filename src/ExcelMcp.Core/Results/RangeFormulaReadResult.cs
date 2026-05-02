namespace ExcelMcp.Core.Results;

public sealed record RangeFormulaReadResult(
    string SheetName,
    string Address,
    IReadOnlyList<IReadOnlyList<string?>> Formulas);
