using ExcelMcp.Bridge.Services;
using ExcelMcp.Core.Logging;
using ExcelMcp.ToolHost.Mcp;

namespace ExcelMcp.ToolHost;

public static class Program
{
    [STAThread]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static async Task<int> Main(string[] args)
    {
        HostOptions options;
        try
        {
            options = HostOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GridPilot MCP configuration error: {ex.Message}");
            return 2;
        }

        try
        {
            await using var logger = CreateLogger(options);
            logger.LogInfo(nameof(Program), "host_starting", new Dictionary<string, object?>
            {
                ["sessionMode"] = options.SessionMode.ToString(),
                ["attachTarget"] = options.AttachTarget.ToString(),
                ["visible"] = options.Visible,
                ["logLevel"] = options.LogLevel.ToString().ToLowerInvariant(),
                ["logPath"] = options.LogPath
            });

            await using var workbookServices = await WorkbookServiceResolver.CreateAsync(options, logger);
            var server = new StdioMcpServer(
                new McpToolServer(workbookServices, logger),
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                logger);

            await server.RunAsync();
            logger.LogInfo(nameof(Program), "host_stopped");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GridPilot MCP startup error: {ex.Message}");
            return 3;
        }
    }

    private static IGridPilotLogger CreateLogger(HostOptions options) =>
        GridPilotLoggerFactory.Create(options.LogLevel, options.LogPath);
}
