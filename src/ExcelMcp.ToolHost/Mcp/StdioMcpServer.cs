using System.Text;
using System.Text.Json;

namespace ExcelMcp.ToolHost.Mcp;

internal sealed class StdioMcpServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly McpToolServer _toolServer;
    private readonly Stream _input;
    private readonly Stream _output;

    public StdioMcpServer(McpToolServer toolServer, Stream input, Stream output)
    {
        _toolServer = toolServer;
        _input = input;
        _output = output;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var payload = await ReadMessageAsync(cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                return;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var methodElement) || methodElement.GetString() is not { } method)
            {
                continue;
            }

            var id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : default;
            try
            {
                object? result = method switch
                {
                    "initialize" => _toolServer.Initialize(GetRequestedProtocolVersion(root)),
                    "notifications/initialized" => null,
                    "tools/list" => new { tools = _toolServer.ListTools() },
                    "tools/call" => await _toolServer.CallToolAsync(
                        GetToolName(root),
                        GetToolArguments(root),
                        cancellationToken).ConfigureAwait(false),
                    _ => throw new InvalidOperationException($"Unsupported MCP method '{method}'.")
                };

                if (id.ValueKind != JsonValueKind.Undefined)
                {
                    await WriteMessageAsync(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (id.ValueKind != JsonValueKind.Undefined)
            {
                await WriteMessageAsync(new
                {
                    jsonrpc = "2.0",
                    id,
                    error = new
                    {
                        code = -32000,
                        message = ex.Message
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<string?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>();
        var buffer = new byte[1];

        while (true)
        {
            var bytesRead = await _input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return headerBytes.Count == 0 ? null : throw new EndOfStreamException("Unexpected EOF while reading MCP headers.");
            }

            headerBytes.Add(buffer[0]);
            var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            if (headerText.EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                var contentLength = ParseContentLength(headerText);
                var payload = new byte[contentLength];
                var offset = 0;
                while (offset < contentLength)
                {
                    var read = await _input.ReadAsync(payload.AsMemory(offset, contentLength - offset), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new EndOfStreamException("Unexpected EOF while reading MCP payload.");
                    }

                    offset += read;
                }

                return Encoding.UTF8.GetString(payload);
            }
        }
    }

    private async Task WriteMessageAsync(object message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
        await _output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await _output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int ParseContentLength(string headerText)
    {
        foreach (var line in headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line["Content-Length:".Length..].Trim(), out var contentLength))
            {
                return contentLength;
            }
        }

        throw new InvalidOperationException("MCP message is missing a valid Content-Length header.");
    }

    private static string? GetRequestedProtocolVersion(JsonElement root)
    {
        if (root.TryGetProperty("params", out var parameters) &&
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("protocolVersion", out var protocolVersion) &&
            protocolVersion.ValueKind == JsonValueKind.String)
        {
            return protocolVersion.GetString();
        }

        return null;
    }

    private static string GetToolName(JsonElement root)
    {
        if (root.TryGetProperty("params", out var parameters) &&
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("name", out var toolName) &&
            toolName.ValueKind == JsonValueKind.String &&
            toolName.GetString() is { Length: > 0 } name)
        {
            return name;
        }

        throw new InvalidOperationException("MCP tool call is missing a valid tool name.");
    }

    private static JsonElement GetToolArguments(JsonElement root)
    {
        if (root.TryGetProperty("params", out var parameters) &&
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("arguments", out var arguments))
        {
            return arguments.Clone();
        }

        return JsonSerializer.SerializeToElement(new { });
    }
}
