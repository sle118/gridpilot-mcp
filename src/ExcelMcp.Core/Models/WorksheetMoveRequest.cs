namespace ExcelMcp.Core;

public sealed record WorksheetMoveRequest(
    string SheetName,
    string? BeforeSheetName = null,
    string? AfterSheetName = null,
    string? Position = null);
