namespace ExcelMcp.Deployment.SmokeTests;

public sealed record McpSmokeTestProcessStartInfo(
    string Command,
    IReadOnlyList<string> Args,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment);
