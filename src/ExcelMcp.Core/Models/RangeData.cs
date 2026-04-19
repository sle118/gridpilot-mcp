namespace ExcelMcp.Core;

public sealed record RangeData(string SheetName, string Address, object?[,] Values);
