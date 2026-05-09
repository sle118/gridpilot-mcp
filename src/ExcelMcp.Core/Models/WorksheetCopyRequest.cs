namespace ExcelMcp.Core;

public sealed record WorksheetCopyRequest(
    string SheetName,
    string NewSheetName,
    string? BeforeSheetName = null,
    string? AfterSheetName = null,
    string? Position = null);
