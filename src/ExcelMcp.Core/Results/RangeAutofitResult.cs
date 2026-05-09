namespace ExcelMcp.Core.Results;

public sealed record RangeAutofitResult(
    bool Succeeded,
    string WorkbookPath,
    int TargetCount,
    IReadOnlyList<string> AppliedTargets,
    OperationError? Error = null);
