namespace ExcelMcp.Core;

public sealed record ConnectionDetail(
    string Name,
    string Type,
    bool RefreshWithRefreshAll,
    bool? BackgroundQuery,
    bool? EnableRefresh,
    bool? RefreshOnFileOpen,
    bool? SavePassword,
    string? LinkedQueryName,
    IReadOnlyList<string> LoadTargets,
    string DependencyNodeId);

public sealed record ConnectionRenameRequest(string ConnectionName, string NewConnectionName);

public sealed record ConnectionUpdateRequest(
    string ConnectionName,
    bool? RefreshWithRefreshAll = null,
    bool? BackgroundQuery = null,
    bool? EnableRefresh = null,
    bool? RefreshOnFileOpen = null,
    bool? SavePassword = null);
