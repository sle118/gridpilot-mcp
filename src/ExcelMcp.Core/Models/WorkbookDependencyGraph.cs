namespace ExcelMcp.Core;

public static class WorkbookDependencyNodeKinds
{
    public const string Query = "query";
    public const string Connection = "connection";
    public const string Table = "table";
    public const string Name = "name";
}

public static class WorkbookDependencyEdgeKinds
{
    public const string QueryLoadsToTable = "query_loads_to_table";
    public const string QueryUsesConnection = "query_uses_connection";
    public const string TableBackedByQuery = "table_backed_by_query";
    public const string TableUsesConnection = "table_uses_connection";
    public const string NameRefersToTable = "name_refers_to_table";
    public const string NameRefersToName = "name_refers_to_name";
}

public sealed record WorkbookDependencyNode(
    string Id,
    string Kind,
    string Name);

public sealed record WorkbookDependencyEdge(
    string SourceNodeId,
    string TargetNodeId,
    string Kind);

public sealed record WorkbookDependencyGraph(
    IReadOnlyList<WorkbookDependencyNode> Nodes,
    IReadOnlyList<WorkbookDependencyEdge> Edges);
