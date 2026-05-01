using System.Text;

namespace ExcelMcp.ToolProxy;

internal sealed class McpFrameSniffer
{
    private enum SnifferMode
    {
        Detecting,
        FramedHeader,
        FramedBody,
        RawJson
    }

    private readonly List<byte> _rawJsonBuffer = [];
    private readonly List<byte> _headerBuffer = [];
    private byte[]? _payloadBuffer;
    private int _payloadOffset;
    private int? _contentLength;
    private SnifferMode _mode;
    private int _rawJsonDepth;
    private bool _rawJsonInString;
    private bool _rawJsonEscaping;

    public IReadOnlyList<string> Append(ReadOnlySpan<byte> bytes)
    {
        var frames = new List<string>();
        for (var index = 0; index < bytes.Length; index++)
        {
            var current = bytes[index];
            if (_mode == SnifferMode.Detecting)
            {
                if (IsIgnorableLeadingByte(current))
                {
                    continue;
                }

                if (current is (byte)'{' or (byte)'[')
                {
                    _mode = SnifferMode.RawJson;
                    _rawJsonDepth = 1;
                    _rawJsonBuffer.Add(current);
                    continue;
                }

                _mode = SnifferMode.FramedHeader;
            }

            if (_mode == SnifferMode.FramedHeader)
            {
                _headerBuffer.Add(current);
                if (TryCompleteHeader(out var headerText, out var contentLength))
                {
                    _mode = SnifferMode.FramedBody;
                    _contentLength = contentLength;
                    _payloadBuffer = new byte[contentLength];
                    _payloadOffset = 0;
                    if (contentLength == 0)
                    {
                        frames.Add(string.Empty);
                        Reset();
                    }
                }

                continue;
            }

            if (_mode == SnifferMode.FramedBody)
            {
                _payloadBuffer![_payloadOffset++] = current;
                if (_payloadOffset == _contentLength.GetValueOrDefault())
                {
                    frames.Add(Encoding.UTF8.GetString(_payloadBuffer!, 0, _payloadOffset));
                    Reset();
                }

                continue;
            }

            _rawJsonBuffer.Add(current);
            if (_rawJsonEscaping)
            {
                _rawJsonEscaping = false;
                continue;
            }

            if (current == '\\')
            {
                if (_rawJsonInString)
                {
                    _rawJsonEscaping = true;
                }

                continue;
            }

            if (current == '"')
            {
                _rawJsonInString = !_rawJsonInString;
                continue;
            }

            if (_rawJsonInString)
            {
                continue;
            }

            if (current is (byte)'{' or (byte)'[')
            {
                _rawJsonDepth++;
            }
            else if (current is (byte)'}' or (byte)']')
            {
                _rawJsonDepth--;
                if (_rawJsonDepth == 0)
                {
                    frames.Add(Encoding.UTF8.GetString(_rawJsonBuffer.ToArray()));
                    Reset();
                }
            }
        }

        return frames;
    }

    public string DescribeState()
    {
        if (_mode is SnifferMode.Detecting or SnifferMode.FramedHeader)
        {
            var previewLength = Math.Min(_headerBuffer.Count, 120);
            var preview = previewLength == 0
                ? string.Empty
                : Encoding.ASCII.GetString(_headerBuffer.Take(previewLength).ToArray()).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
            return $"awaiting_header bytes={_headerBuffer.Count} preview=\"{preview}\"";
        }

        if (_mode == SnifferMode.FramedBody)
        {
            return $"awaiting_body contentLength={_contentLength.GetValueOrDefault()} received={_payloadOffset}";
        }

        var rawPreviewLength = Math.Min(_rawJsonBuffer.Count, 120);
        var rawPreview = rawPreviewLength == 0
            ? string.Empty
            : Encoding.UTF8.GetString(_rawJsonBuffer.Take(rawPreviewLength).ToArray()).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
        return $"awaiting_raw_json depth={_rawJsonDepth} bytes={_rawJsonBuffer.Count} preview=\"{rawPreview}\"";
    }

    public void Reset()
    {
        _mode = SnifferMode.Detecting;
        _headerBuffer.Clear();
        _rawJsonBuffer.Clear();
        _payloadBuffer = null;
        _payloadOffset = 0;
        _contentLength = null;
        _rawJsonDepth = 0;
        _rawJsonInString = false;
        _rawJsonEscaping = false;
    }

    private bool TryCompleteHeader(out string headerText, out int contentLength)
    {
        headerText = string.Empty;
        contentLength = 0;
        if (!EndsWith(_headerBuffer, "\r\n\r\n"u8) && !EndsWith(_headerBuffer, "\n\n"u8))
        {
            return false;
        }

        headerText = Encoding.ASCII.GetString(_headerBuffer.ToArray());
        var normalizedHeader = headerText.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in normalizedHeader.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength))
            {
                return true;
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

    private static bool IsIgnorableLeadingByte(byte value) =>
        value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
