using ExcelMcp.Bridge.Services;
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
            await using var workbookServices = await WorkbookServiceResolver.CreateAsync(options);
            var server = new StdioMcpServer(
                new McpToolServer(workbookServices),
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
