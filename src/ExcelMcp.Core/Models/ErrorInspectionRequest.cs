namespace ExcelMcp.Core;

public sealed record ErrorInspectionRequest(string Scope, string? SheetName = null, string? Address = null);
