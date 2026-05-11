namespace ExcelMcp.Deployment.SmokeTests;

public interface IMcpSmokeTestProcessLauncher
{
    IMcpSmokeTestProcess Launch(McpSmokeTestProcessStartInfo startInfo);
}
