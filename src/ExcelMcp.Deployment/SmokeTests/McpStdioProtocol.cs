using System.Text;
using System.Text.Json;

namespace ExcelMcp.Deployment.SmokeTests;

internal static class McpStdioProtocol
{
    private const int MaxHeaderBytes = 16 * 1024;

    public static async Task WriteAsync(
        Stream stream,
        object message,
        McpSmokeTestTransportMode transportMode,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        if (transportMode == McpSmokeTestTransportMode.Framed)
        {
            var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<McpStdioMessage> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var first = await ReadFirstNonWhitespaceByteAsync(stream, cancellationToken).ConfigureAwait(false);
        if (first is (byte)'{' or (byte)'[')
        {
            return new McpStdioMessage(
                await ReadRawJsonAsync(stream, first, cancellationToken).ConfigureAwait(false),
                McpSmokeTestTransportMode.RawJson);
        }

        if (first is (byte)'C' or (byte)'c')
        {
            return new McpStdioMessage(
                await ReadFramedAsync(stream, first, cancellationToken).ConfigureAwait(false),
                McpSmokeTestTransportMode.Framed);
        }

        throw new McpStdoutPollutionException(
            $"MCP stdout contained non-JSON-RPC text before a response: byte 0x{first:x2}.");
    }

    private static async Task<byte> ReadFirstNonWhitespaceByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("MCP process closed stdout before a response was received.");
            }

            if (buffer[0] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return buffer[0];
        }
    }

    private static async Task<string> ReadFramedAsync(
        Stream stream,
        byte first,
        CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte> { first };
        var buffer = new byte[1];

        while (true)
        {
            if (TryParseHeader(headerBytes, out var contentLength))
            {
                var payload = new byte[contentLength];
                var offset = 0;
                while (offset < payload.Length)
                {
                    var read = await stream.ReadAsync(payload.AsMemory(offset, payload.Length - offset), cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        throw new EndOfStreamException("MCP process closed stdout while a framed response body was being read.");
                    }

                    offset += read;
                }

                return Encoding.UTF8.GetString(payload);
            }

            if (headerBytes.Count >= MaxHeaderBytes)
            {
                throw new McpSmokeTestProtocolException("MCP framed response header exceeded the maximum header size.");
            }

            var bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("MCP process closed stdout while a framed response header was being read.");
            }

            headerBytes.Add(buffer[0]);
        }
    }

    private static bool TryParseHeader(List<byte> headerBytes, out int contentLength)
    {
        contentLength = 0;
        if (!EndsWith(headerBytes, "\r\n\r\n"u8) && !EndsWith(headerBytes, "\n\n"u8))
        {
            return false;
        }

        var header = Encoding.ASCII.GetString(headerBytes.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength) &&
                contentLength >= 0)
            {
                return true;
            }
        }

        throw new McpSmokeTestProtocolException("MCP framed response is missing a valid Content-Length header.");
    }

    private static async Task<string> ReadRawJsonAsync(
        Stream stream,
        byte first,
        CancellationToken cancellationToken)
    {
        var messageBytes = new List<byte> { first };
        var nestingDepth = 1;
        var inString = false;
        var escaping = false;
        var buffer = new byte[1];

        while (nestingDepth > 0)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("MCP process closed stdout while a raw JSON response was being read.");
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
            }
            else if (current is (byte)'}' or (byte)']')
            {
                nestingDepth--;
            }
        }

        return Encoding.UTF8.GetString(messageBytes.ToArray());
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
}
