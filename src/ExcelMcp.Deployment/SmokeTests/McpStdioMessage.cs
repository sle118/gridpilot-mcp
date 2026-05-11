namespace ExcelMcp.Deployment.SmokeTests;

internal sealed record McpStdioMessage(string Payload, McpSmokeTestTransportMode TransportMode);
