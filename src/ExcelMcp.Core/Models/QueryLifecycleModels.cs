namespace ExcelMcp.Core;

public static class QueryLoadModes
{
    public const string None = "none";
    public const string Worksheet = "worksheet";
    public const string DataModel = "dataModel";
    public const string WorksheetAndDataModel = "worksheetAndDataModel";
}

public sealed record QueryCreateRequest(
    string QueryName,
    string Formula,
    string LoadMode = QueryLoadModes.None,
    string? DestinationSheetName = null,
    string? DestinationAddress = null);

public sealed record QueryRenameRequest(string QueryName, string NewQueryName);

public sealed record QueryDetail(
    string Name,
    string Formula,
    string? Description,
    string LoadMode,
    string? DestinationSheetName,
    string? DestinationAddress,
    string? ConnectionName,
    string DependencyNodeId);
