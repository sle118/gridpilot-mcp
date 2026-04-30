namespace ExcelMcp.Core.Results;

public sealed record TableMutationResult(
    bool Succeeded,
    string WorkbookPath,
    string TableName,
    string Action,
    string? SheetName = null,
    string? Address = null,
    int? RowCount = null,
    bool? HasHeaders = null,
    bool? ShowTotals = null,
    OperationError? Error = null);
