namespace ExcelMcp.Core;

public sealed record RangeFormulaWriteRequest(IReadOnlyList<RangeFormulaWriteTarget> Writes);

public sealed record RangeFormulaWriteTarget(string SheetName, string Address, string?[,] Formulas)
{
    public string Identifier => $"{SheetName}!{Address}";
}
