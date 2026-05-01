using ExcelMcp.Core;
using ExcelMcp.Core.Logging;
using ExcelMcp.Core.Results;
using ExcelMcp.ToolHost;
using ExcelMcp.ToolHost.Mcp;
using System.Text;
using System.Text.Json;

namespace ExcelMcp.IntegrationTests;

public sealed class StdioMcpServerTests
{
    [Theory]
    [InlineData("\r\n\r\n")]
    [InlineData("\n\n")]
    public async Task RunAsync_ParsesInitializeRequest_ForBothHeaderTerminators(string headerTerminator)
    {
        var output = new MemoryStream();
        var server = new StdioMcpServer(
            new McpToolServer(new ConnectionAwareResolverForStdio()),
            BuildInputStream(headerTerminator),
            output);

        await server.RunAsync();

        output.Position = 0;
        var responseText = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("Content-Length:", responseText, StringComparison.Ordinal);
        Assert.Contains("\"protocolVersion\":\"2024-11-05\"", responseText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ParsesHeaderlessInitializeRequest()
    {
        var output = new MemoryStream();
        var tempDirectory = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempDirectory, "runtime.log");
        await using (var logger = GridPilotLoggerFactory.Create(GridPilotLogLevel.Info, logPath))
        {
            var server = new StdioMcpServer(
                new McpToolServer(new ConnectionAwareResolverForStdio(), logger),
                BuildRawJsonInputStream(),
                output,
                logger);

            await server.RunAsync();
        }

        output.Position = 0;
        var responseText = Encoding.UTF8.GetString(output.ToArray());
        Assert.DoesNotContain("Content-Length:", responseText, StringComparison.Ordinal);
        Assert.EndsWith("\n", responseText, StringComparison.Ordinal);
        var trimmedResponse = responseText.TrimEnd('\r', '\n');
        Assert.StartsWith("{", trimmedResponse, StringComparison.Ordinal);
        Assert.Contains("\"protocolVersion\":\"2025-06-18\"", trimmedResponse, StringComparison.Ordinal);
        Assert.True(File.Exists(logPath));
        Assert.Contains("transport_detected", await File.ReadAllTextAsync(logPath), StringComparison.Ordinal);
    }

    private static MemoryStream BuildInputStream(string headerTerminator)
    {
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05"
            }
        });

        var message = $"Content-Length: {Encoding.UTF8.GetByteCount(payload)}{headerTerminator}{payload}";
        return new MemoryStream(Encoding.UTF8.GetBytes(message));
    }

    private static MemoryStream BuildRawJsonInputStream()
    {
        var payload = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 0,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new
                {
                    elicitation = new
                    {
                        form = new { }
                    }
                },
                clientInfo = new
                {
                    name = "codex-mcp-client",
                    version = "0.125.0"
                }
            }
        });

        return new MemoryStream(Encoding.UTF8.GetBytes(payload));
    }

    private sealed class ConnectionAwareResolverForStdio : IWorkbookServiceResolver
    {
        public Task<T> ExecuteAsync<T>(WorkbookTarget target, Func<ResolvedWorkbookContext, Task<T>> action, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WorkbookSummary>> ListOpenWorkbooksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookSummary>>(Array.Empty<WorkbookSummary>());

        public Task<WorkbookConnectionResult> ConnectAsync(WorkbookConnectionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WorkbookConnectionResult> CreateWorkbookAsync(WorkbookCreateRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WorkbookConnectionInfo>> ListConnectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkbookConnectionInfo>>(Array.Empty<WorkbookConnectionInfo>());

        public Task<WorkbookConnectionInfo> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WorkbookDisconnectResult> DisconnectAsync(string connectionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AttachedMutationApprovalGrantResult> GrantAttachedMutationApprovalAsync(WorkbookTarget target, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AttachedMutationApprovalRevokeResult> RevokeAttachedMutationApprovalAsync(WorkbookTarget target, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
