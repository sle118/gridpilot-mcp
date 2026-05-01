namespace ExcelMcp.ToolProxy;

internal sealed record ProxyOptions(
    string LogPath,
    string Label,
    string Command,
    IReadOnlyList<string> CommandArguments)
{
    public static ProxyOptions Parse(string[] args)
    {
        string? logPath = null;
        string label = "mcp-proxy";
        var separatorIndex = Array.IndexOf(args, "--");
        if (separatorIndex < 0 || separatorIndex == args.Length - 1)
        {
            throw new InvalidOperationException("Proxy usage requires '--' followed by the wrapped MCP command.");
        }

        for (var index = 0; index < separatorIndex; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--log-path", StringComparison.OrdinalIgnoreCase) && index + 1 < separatorIndex)
            {
                logPath = args[++index];
                continue;
            }

            if (string.Equals(argument, "--label", StringComparison.OrdinalIgnoreCase) && index + 1 < separatorIndex)
            {
                label = args[++index];
                continue;
            }

            throw new InvalidOperationException($"Unsupported proxy argument '{argument}'.");
        }

        var command = args[separatorIndex + 1];
        var commandArguments = args.Skip(separatorIndex + 2).ToArray();
        logPath ??= Path.Combine(Environment.CurrentDirectory, ".tmp", "mcp-proxy", $"{label}.log");
        return new ProxyOptions(logPath, label, command, commandArguments);
    }
}
