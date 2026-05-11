namespace ExcelMcp.Deployment.SmokeTests;

public sealed record McpSmokeTestStepResult(
    string Id,
    string Name,
    McpSmokeTestStatus Status,
    string Message,
    string SuggestedNextStep);
