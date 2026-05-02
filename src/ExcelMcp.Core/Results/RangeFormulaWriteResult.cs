namespace ExcelMcp.Core.Results;

public sealed record RangeFormulaWriteResult(
    bool Succeeded,
    string WorkbookPath,
    int WriteCount,
    IReadOnlyList<string> AppliedWrites,
    OperationError? Error = null);
