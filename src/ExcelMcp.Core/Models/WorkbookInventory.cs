namespace ExcelMcp.Core;

public sealed record WorkbookInventory(
    IReadOnlyList<SheetSummary> Sheets,
    IReadOnlyList<TableSummary> Tables,
    IReadOnlyList<QuerySummary> Queries,
    IReadOnlyList<ConnectionSummary> Connections);
