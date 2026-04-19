namespace ExcelMcp.Core.Results;

public sealed record RefreshResult(
    bool Succeeded,
    string Target,
    string Mode,
    TimeSpan Duration,
    OperationError? Error = null);
