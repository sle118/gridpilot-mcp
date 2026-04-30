namespace ExcelMcp.Core.Results;

public sealed record TableReadResult(
    string TableName,
    string SheetName,
    string Address,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    bool HasTotalsRow);
