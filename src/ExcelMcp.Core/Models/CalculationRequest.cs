namespace ExcelMcp.Core;

public sealed record CalculationRequest(string Scope, string? SheetName = null, string? Address = null);
