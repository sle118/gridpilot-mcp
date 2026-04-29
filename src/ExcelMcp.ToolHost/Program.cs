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
        var options = HostOptions.Parse(args);
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
}
