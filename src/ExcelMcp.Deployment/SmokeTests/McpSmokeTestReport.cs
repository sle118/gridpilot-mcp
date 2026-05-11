namespace ExcelMcp.Deployment.SmokeTests;

public sealed record McpSmokeTestReport(
    IReadOnlyList<McpSmokeTestStepResult> Results,
    McpSmokeTestTransportMode? DetectedTransportMode,
    int? ExitCode,
    bool WasKilled,
    string StderrTail,
    IReadOnlyList<string> MissingToolNames)
{
    public bool IsSuccess => Results.All(result => result.Status is McpSmokeTestStatus.Success or McpSmokeTestStatus.Warning);
}
