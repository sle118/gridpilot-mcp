namespace ExcelMcp.Core.Results;

public sealed record WorksheetMutationResult(
    bool Succeeded,
    string WorkbookPath,
    string SheetName,
    string Action,
    string? NewSheetName = null,
    OperationError? Error = null);
