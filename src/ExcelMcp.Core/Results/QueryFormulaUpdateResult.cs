namespace ExcelMcp.Core.Results;

public sealed record QueryFormulaUpdateResult(
    bool Succeeded,
    string WorkbookPath,
    string QueryName,
    OperationError? Error = null);
