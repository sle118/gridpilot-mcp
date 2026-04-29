using ExcelMcp.Bridge.Services;
using ExcelMcp.ComAdapter;
using ExcelMcp.Core.Abstractions;
using ExcelMcp.ToolHost.Mcp;

namespace ExcelMcp.ToolHost;

public static class Program
{
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

        Console.Error.WriteLine(options.ToStartupSummary());

        try
        {
            await using IExcelSession session = options.SessionMode switch
            {
                SessionMode.Attach => ExcelApplicationSession.AttachToRunning(),
                _ => ExcelApplicationSession.CreateNew(options.Visible)
            };

            var workbookService = new WorkbookService(session);
            var server = new StdioMcpServer(
                new McpToolServer(workbookService),
                Console.OpenStandardInput(),
                Console.OpenStandardOutput());

            await server.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GridPilot MCP startup error: {ex.Message}");
            return 3;
        }
    }
}
