namespace ExcelMcp.Core;

public sealed record NameSummary(
    string Name,
    string Scope,
    string? SheetName,
    string RefersTo,
    string? Address);
