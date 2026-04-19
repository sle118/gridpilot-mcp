namespace ExcelMcp.Core;

public sealed record QueryDefinition(string Name, string Formula, string? Description = null);
