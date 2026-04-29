using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExcelMcp.ToolHost.Mcp;

public sealed record McpInitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] object Capabilities,
    [property: JsonPropertyName("serverInfo")] object ServerInfo);

public sealed record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema);

public sealed record McpToolCallResult(
    [property: JsonPropertyName("content")] IReadOnlyList<object> Content,
    [property: JsonPropertyName("structuredContent")] JsonElement StructuredContent,
    [property: JsonPropertyName("isError")] bool IsError = false);
