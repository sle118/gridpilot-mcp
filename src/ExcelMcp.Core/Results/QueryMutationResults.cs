namespace ExcelMcp.Core.Results;

public sealed record QueryMutationResult(
    bool Succeeded,
    string WorkbookPath,
    string QueryName,
    string Action,
    string? NewQueryName = null,
    string? LoadMode = null,
    string? DestinationSheetName = null,
    string? DestinationAddress = null,
    string? ConnectionName = null,
    OperationError? Error = null);

public sealed record QueryDeleteResult(
    bool Succeeded,
    string WorkbookPath,
    string QueryName,
    string? ConnectionName = null,
    OperationError? Error = null);
