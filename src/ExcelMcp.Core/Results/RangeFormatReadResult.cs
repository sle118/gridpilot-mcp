namespace ExcelMcp.Core.Results;

public sealed record RangeFormatReadResult(
    bool Succeeded,
    string SheetName,
    string Address,
    ExcelMcp.Core.RangeFormatSnapshot Format,
    IReadOnlyList<string> MixedProperties,
    OperationError? Error = null);
