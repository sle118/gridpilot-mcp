namespace ExcelMcp.Core;

public sealed record RangeFormatData(
    string SheetName,
    string Address,
    RangeFormatSnapshot Format,
    IReadOnlyList<string> MixedProperties);
