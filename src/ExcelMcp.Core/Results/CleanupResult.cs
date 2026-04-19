namespace ExcelMcp.Core.Results;

public sealed record CleanupResult(
    int DeletedCount,
    IReadOnlyList<string> DeletedNames);
