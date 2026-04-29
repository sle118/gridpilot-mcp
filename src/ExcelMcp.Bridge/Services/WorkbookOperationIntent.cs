namespace ExcelMcp.Bridge.Services;

public enum WorkbookOperationIntent
{
    ReadOnly = 0,
    Mutating = 1,
    DiagnosticTempWrite = 2
}
