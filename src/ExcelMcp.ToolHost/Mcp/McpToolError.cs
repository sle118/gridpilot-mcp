using ExcelMcp.ToolHost.Diagnostics;
using System.Text.Json.Serialization;

namespace ExcelMcp.ToolHost.Mcp;

public sealed record McpToolError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("detail")] string? Detail = null,
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("remediation")] ToolRemediationHint? Remediation = null);
