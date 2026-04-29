namespace ExcelMcp.Core.Results;

public sealed record RangeReadResult(
    string SheetName,
    string Address,
    IReadOnlyList<IReadOnlyList<object?>> Values);
