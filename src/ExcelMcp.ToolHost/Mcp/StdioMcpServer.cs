using System.Text;
using System.Text.Json;
using ExcelMcp.Core.Logging;

namespace ExcelMcp.ToolHost.Mcp;

internal sealed class StdioMcpServer
{
    private enum StdioTransportMode
    {
        FramedMcp,
        RawJson
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly McpToolServer _toolServer;
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly IGridPilotLogger _logger;
    private StdioTransportMode _transportMode = StdioTransportMode.FramedMcp;

    public StdioMcpServer(McpToolServer toolServer, Stream input, Stream output, IGridPilotLogger? logger = null)
    {
        _toolServer = toolServer;
        _input = input;
        _output = output;
        _logger = logger ?? GridPilotNullLogger.Instance;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var payload = await ReadMessageAsync(cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                _logger.LogInfo(nameof(StdioMcpServer), "stdio_eof");
                return;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var methodElement) || methodElement.GetString() is not { } method)
            {
                _logger.LogDebug(nameof(StdioMcpServer), "message_missing_method");
                continue;
            }

            var id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : default;
            try
            {
                _logger.LogDebug(nameof(StdioMcpServer), "message_received", new Dictionary<string, object?>
                {
                    ["method"] = method,
                    ["hasId"] = id.ValueKind != JsonValueKind.Undefined,
                    ["transportMode"] = _transportMode.ToString()
                });

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
                    _logger.LogTrace(nameof(StdioMcpServer), "message_written", new Dictionary<string, object?>
                    {
                        ["method"] = method,
                        ["transportMode"] = _transportMode.ToString()
                    });
                }
            }
            catch (Exception ex) when (id.ValueKind != JsonValueKind.Undefined)
            {
                _logger.LogInfo(nameof(StdioMcpServer), "message_failed", new Dictionary<string, object?>
                {
                    ["method"] = method,
                    ["transportMode"] = _transportMode.ToString()
                }, ex);
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
        var buffer = new byte[1];

        while (true)
        {
            var bytesRead = await _input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return null;
            }

            var current = buffer[0];
            if (IsIgnorableLeadingByte(current))
            {
                continue;
            }

            if (current is (byte)'{' or (byte)'[')
            {
                _transportMode = StdioTransportMode.RawJson;
                _logger.LogInfo(nameof(StdioMcpServer), "transport_detected", new Dictionary<string, object?>
                {
                    ["transportMode"] = _transportMode.ToString()
                });
                return await ReadRawJsonMessageAsync(current, cancellationToken).ConfigureAwait(false);
            }

            _transportMode = StdioTransportMode.FramedMcp;
            _logger.LogInfo(nameof(StdioMcpServer), "transport_detected", new Dictionary<string, object?>
            {
                ["transportMode"] = _transportMode.ToString()
            });
            return await ReadFramedMessageAsync(
                new List<byte> { current },
                buffer,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> ReadFramedMessageAsync(
        List<byte> headerBytes,
        byte[] singleByteBuffer,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (TryParseHeader(headerBytes, out var headerText))
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

            var bytesRead = await _input.ReadAsync(singleByteBuffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected EOF while reading MCP headers.");
            }

            headerBytes.Add(singleByteBuffer[0]);
        }
    }

    private async Task<string> ReadRawJsonMessageAsync(byte firstByte, CancellationToken cancellationToken)
    {
        var messageBytes = new List<byte> { firstByte };
        var nestingDepth = 1;
        var inString = false;
        var escaping = false;
        var buffer = new byte[1];

        while (nestingDepth > 0)
        {
            var bytesRead = await _input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Unexpected EOF while reading raw JSON MCP payload.");
            }

            var current = buffer[0];
            messageBytes.Add(current);

            if (escaping)
            {
                escaping = false;
                continue;
            }

            if (current == '\\')
            {
                if (inString)
                {
                    escaping = true;
                }

                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current is (byte)'{' or (byte)'[')
            {
                nestingDepth++;
                continue;
            }

            if (current is (byte)'}' or (byte)']')
            {
                nestingDepth--;
            }
        }

        return Encoding.UTF8.GetString(messageBytes.ToArray());
    }

    private static bool IsIgnorableLeadingByte(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private static bool TryParseHeader(List<byte> headerBytes, out string headerText)
    {
        headerText = string.Empty;
        if (headerBytes.Count < 2)
        {
            return false;
        }

        if (EndsWith(headerBytes, "\r\n\r\n"u8) || EndsWith(headerBytes, "\n\n"u8))
        {
            headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            return true;
        }

        return false;
    }

    private async Task WriteMessageAsync(object message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (_transportMode == StdioTransportMode.FramedMcp)
        {
            var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
            await _output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _output.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int ParseContentLength(string headerText)
    {
        foreach (var line in headerText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line["Content-Length:".Length..].Trim(), out var contentLength))
            {
                return contentLength;
            }
        }

        throw new InvalidOperationException("MCP message is missing a valid Content-Length header.");
    }

    private static bool EndsWith(List<byte> source, ReadOnlySpan<byte> suffix)
    {
        if (source.Count < suffix.Length)
        {
            return false;
        }

        var offset = source.Count - suffix.Length;
        for (var index = 0; index < suffix.Length; index++)
        {
            if (source[offset + index] != suffix[index])
            {
                return false;
            }
        }

        return true;
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
