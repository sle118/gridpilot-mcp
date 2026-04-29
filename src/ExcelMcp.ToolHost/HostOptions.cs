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
        var mode = ReadModeFromEnvironment();
        var visible = ReadVisibleFromEnvironment();

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

        return new HostOptions(mode, visible);
    }

    public string ToStartupSummary() =>
        $"GridPilot MCP starting with sessionMode={SessionModeToString(SessionMode)}, visible={Visible.ToString().ToLowerInvariant()}.";

    private static SessionMode ParseMode(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "attach" => SessionMode.Attach,
            "create-new" => SessionMode.CreateNew,
            "new" => SessionMode.CreateNew,
            _ => throw new InvalidOperationException($"Unsupported session mode '{value}'. Use 'attach' or 'create-new'.")
        };

    private static SessionMode ReadModeFromEnvironment()
    {
        var envMode = Environment.GetEnvironmentVariable("GRIDPILOT_SESSION_MODE");
        return string.IsNullOrWhiteSpace(envMode) ? SessionMode.CreateNew : ParseMode(envMode);
    }

    private static bool ReadVisibleFromEnvironment()
    {
        var envVisible = Environment.GetEnvironmentVariable("GRIDPILOT_SESSION_VISIBLE");
        return string.Equals(envVisible, "1", StringComparison.Ordinal);
    }

    private static string SessionModeToString(SessionMode mode) =>
        mode switch
        {
            SessionMode.Attach => "attach",
            _ => "create-new"
        };
}
