namespace ExcelMcp.Core;

public sealed record RangeWriteRequest(IReadOnlyList<RangeWriteTarget> Writes);

public sealed record RangeWriteTarget(string SheetName, string Address, object?[,] Values)
{
    public string Identifier => $"{SheetName}!{Address}";
}
