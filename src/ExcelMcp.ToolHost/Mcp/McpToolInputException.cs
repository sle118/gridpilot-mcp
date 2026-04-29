namespace ExcelMcp.ToolHost.Mcp;

internal sealed class McpToolInputException : Exception
{
    public McpToolInputException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
