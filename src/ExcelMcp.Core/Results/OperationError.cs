namespace ExcelMcp.Core.Results;

public sealed record OperationError(
    string Code,
    string Message,
    string? Detail = null,
    string? Source = null);
