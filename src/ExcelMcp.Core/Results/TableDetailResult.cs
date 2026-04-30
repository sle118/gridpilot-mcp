namespace ExcelMcp.Core.Results;

public sealed record TableDetailResult(
    string TableName,
    string SheetName,
    string Address,
    IReadOnlyList<string> Headers,
    int RowCount,
    int ColumnCount,
    bool HasHeaders,
    bool HasTotalsRow,
    bool IsQueryBacked,
    string? QueryName);
