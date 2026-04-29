using ExcelMcp.Core;

namespace ExcelMcp.ToolHost;

internal enum SessionMode
{
    CreateNew = 0,
    Attach = 1
}

internal sealed record HostOptions(SessionMode SessionMode, SessionAttachTargetMode AttachTarget, bool Visible)
{
    public static HostOptions Parse(string[] args)
    {
        var mode = ReadModeFromEnvironment();
        var attachTarget = ReadAttachTargetFromEnvironment();
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
                continue;
            }

            if (string.Equals(argument, "--attach-target", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                index++;
                attachTarget = ParseAttachTarget(args[index]);
            }
        }

        return new HostOptions(mode, attachTarget, visible);
    }

    public string ToStartupSummary() =>
        $"GridPilot MCP starting with sessionMode={SessionModeToString(SessionMode)}, attachTarget={AttachTargetToString(AttachTarget)}, visible={Visible.ToString().ToLowerInvariant()}.";

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

    private static SessionAttachTargetMode ParseAttachTarget(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "any" => SessionAttachTargetMode.AnyRunningInstance,
            "any-running" => SessionAttachTargetMode.AnyRunningInstance,
            "workbook" => SessionAttachTargetMode.WorkbookOwner,
            "workbook-owner" => SessionAttachTargetMode.WorkbookOwner,
            _ => throw new InvalidOperationException($"Unsupported attach target '{value}'. Use 'workbook-owner' or 'any-running'.")
        };

    private static SessionAttachTargetMode ReadAttachTargetFromEnvironment()
    {
        var envTarget = Environment.GetEnvironmentVariable("GRIDPILOT_ATTACH_TARGET");
        return string.IsNullOrWhiteSpace(envTarget) ? SessionAttachTargetMode.WorkbookOwner : ParseAttachTarget(envTarget);
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

    private static string AttachTargetToString(SessionAttachTargetMode target) =>
        target switch
        {
            SessionAttachTargetMode.AnyRunningInstance => "any-running",
            _ => "workbook-owner"
        };
}
