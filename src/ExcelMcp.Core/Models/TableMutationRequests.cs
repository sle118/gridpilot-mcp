namespace ExcelMcp.Core;

public sealed record TableCreateRequest(
    string TableName,
    string SheetName,
    string Address,
    bool HasHeaders = true);

public sealed record TableResizeRequest(
    string TableName,
    string SheetName,
    string Address);

public sealed record TableRowsWriteRequest(
    string TableName,
    object?[,] Values);

public sealed record TableOptionsUpdateRequest(
    string TableName,
    bool? HasHeaders = null,
    bool? ShowTotals = null);
