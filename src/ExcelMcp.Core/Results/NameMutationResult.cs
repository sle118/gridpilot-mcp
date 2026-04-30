namespace ExcelMcp.Core.Results;

public sealed record NameMutationResult(
    bool Succeeded,
    string WorkbookPath,
    string Name,
    string Action,
    string Scope,
    string? SheetName = null,
    string? RefersTo = null,
    OperationError? Error = null);
