namespace ExcelMcp.Core.Results;

public sealed record WorksheetLayoutMutationResult(
    bool Succeeded,
    string WorkbookPath,
    string SheetName,
    string Action,
    string? NewSheetName = null,
    string? BeforeSheetName = null,
    string? AfterSheetName = null,
    string? Position = null,
    string? Visibility = null,
    OperationError? Error = null);
