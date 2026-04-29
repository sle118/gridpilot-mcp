namespace ExcelMcp.Core.Results;

public sealed record RangeWriteResult(
    bool Succeeded,
    string WorkbookPath,
    int WriteCount,
    IReadOnlyList<string> AppliedWrites,
    OperationError? Error = null);
