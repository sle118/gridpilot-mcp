namespace ExcelMcp.Core;

public sealed record RangeClearRequest(IReadOnlyList<RangeClearTarget> Clears);

public sealed record RangeClearTarget(string SheetName, string Address)
{
    public string Identifier => $"{SheetName}!{Address}";
}
