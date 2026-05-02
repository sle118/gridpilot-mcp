namespace ExcelMcp.Core.Results;

public sealed record RangeClearResult(
    bool Succeeded,
    string WorkbookPath,
    int ClearCount,
    IReadOnlyList<string> AppliedClears,
    OperationError? Error = null);
