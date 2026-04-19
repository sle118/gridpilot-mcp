namespace ExcelMcp.Core;

public sealed record TableSummary(string SheetName, string TableName, string Address, bool IsQueryBacked, string? QueryName);
