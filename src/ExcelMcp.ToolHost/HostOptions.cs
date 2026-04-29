namespace ExcelMcp.ToolHost;

internal enum SessionMode
{
    CreateNew = 0,
    Attach = 1
}

internal sealed record HostOptions(SessionMode SessionMode, bool Visible)
{
    public static HostOptions Parse(string[] args)
    {
        var mode = SessionMode.CreateNew;
        var visible = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--visible", StringComparison.OrdinalIgnoreCase))
            {
                visible = true;
                continue;
            }

            if (string.Equals(argument, "--session-mode", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                index++;
                mode = ParseMode(args[index]);
            }
        }

        var envMode = Environment.GetEnvironmentVariable("GRIDPILOT_SESSION_MODE");
        if (!string.IsNullOrWhiteSpace(envMode))
        {
            mode = ParseMode(envMode);
        }

        var envVisible = Environment.GetEnvironmentVariable("GRIDPILOT_SESSION_VISIBLE");
        if (string.Equals(envVisible, "1", StringComparison.Ordinal))
        {
            visible = true;
        }

        return new HostOptions(mode, visible);
    }

    private static SessionMode ParseMode(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "attach" => SessionMode.Attach,
            "create-new" => SessionMode.CreateNew,
            "new" => SessionMode.CreateNew,
            _ => SessionMode.CreateNew
        };
}
