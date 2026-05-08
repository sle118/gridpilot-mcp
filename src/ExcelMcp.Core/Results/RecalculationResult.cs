namespace ExcelMcp.Core.Results;

public sealed record RecalculationResult(
    bool Succeeded,
    string WorkbookPath,
    string Scope,
    string? SheetName,
    string? Address,
    TimeSpan Duration,
    OperationError? Error = null);
