namespace ExcelMcp.Core.Results;

public sealed record ProbeResult(
    bool Succeeded,
    string TargetQuery,
    string TempQuery,
    RangeReadResult? Preview = null,
    OperationError? Error = null);
