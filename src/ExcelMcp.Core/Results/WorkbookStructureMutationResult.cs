namespace ExcelMcp.Core.Results;

public sealed record WorkbookStructureMutationResult(
    bool Succeeded,
    string WorkbookPath,
    string Action,
    string? Visibility = null,
    string? Mode = null,
    bool? ProtectStructure = null,
    bool? ProtectWindows = null,
    OperationError? Error = null);
