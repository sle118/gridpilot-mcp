namespace ExcelMcp.Deployment.SmokeTests;

internal sealed class McpStdoutPollutionException : Exception
{
    public McpStdoutPollutionException(string message)
        : base(message)
    {
    }
}
