namespace ExcelMcp.Core.Results;

public sealed record ConnectionMutationResult(
    bool Succeeded,
    string WorkbookPath,
    string ConnectionName,
    string Action,
    string? NewConnectionName = null,
    bool? RefreshWithRefreshAll = null,
    bool? BackgroundQuery = null,
    bool? EnableRefresh = null,
    bool? RefreshOnFileOpen = null,
    bool? SavePassword = null,
    OperationError? Error = null);
