namespace ExcelMcp.Deployment.SmokeTests;

internal sealed class McpSmokeTestProtocolException : Exception
{
    public McpSmokeTestProtocolException(string message)
        : base(message)
    {
    }
}
