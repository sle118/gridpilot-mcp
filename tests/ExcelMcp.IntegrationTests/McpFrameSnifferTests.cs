using ExcelMcp.ToolProxy;
using System.Text;

namespace ExcelMcp.IntegrationTests;

public sealed class McpFrameSnifferTests
{
    [Theory]
    [InlineData("\r\n\r\n")]
    [InlineData("\n\n")]
    public void Append_ParsesCompleteFrame_ForSupportedHeaderTerminators(string terminator)
    {
        var sniffer = new McpFrameSniffer();
        var body = "{\"jsonrpc\":\"2.0\",\"id\":1}";
        var payload = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(body)}{terminator}{body}");

        var frames = sniffer.Append(payload);

        var frame = Assert.Single(frames);
        Assert.Equal(body, frame);
    }

    [Fact]
    public void Append_ParsesFrameAcrossMultipleChunks()
    {
        var sniffer = new McpFrameSniffer();
        var body = "{\"jsonrpc\":\"2.0\",\"id\":1}";
        var payload = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}");

        var frames1 = sniffer.Append(payload.AsSpan(0, 10));
        var frames2 = sniffer.Append(payload.AsSpan(10, payload.Length - 10));

        Assert.Empty(frames1);
        var frame = Assert.Single(frames2);
        Assert.Equal(body, frame);
    }

    [Fact]
    public void Append_ParsesRawJsonAcrossMultipleChunks()
    {
        var sniffer = new McpFrameSniffer();
        var body = "{\"jsonrpc\":\"2.0\",\"id\":0,\"result\":{\"protocolVersion\":\"2025-06-18\"}}";
        var payload = Encoding.UTF8.GetBytes(body);

        var frames1 = sniffer.Append(payload.AsSpan(0, 12));
        var frames2 = sniffer.Append(payload.AsSpan(12, payload.Length - 12));

        Assert.Empty(frames1);
        var frame = Assert.Single(frames2);
        Assert.Equal(body, frame);
    }
}
