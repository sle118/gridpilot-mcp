namespace ExcelMcp.Core;

public sealed record RangeAutofitRequest(IReadOnlyList<RangeAutofitTarget> Targets);

public sealed record RangeAutofitTarget(string SheetName, string Address, string Dimension)
{
    public string Identifier => $"{SheetName}!{Address}";
}
