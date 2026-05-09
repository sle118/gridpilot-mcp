namespace ExcelMcp.Core;

public sealed record RangeFormatWriteRequest(IReadOnlyList<RangeFormatWriteTarget> Writes);

public sealed record RangeFormatWriteTarget(string SheetName, string Address, RangeFormatPatch Format)
{
    public string Identifier => $"{SheetName}!{Address}";
}
