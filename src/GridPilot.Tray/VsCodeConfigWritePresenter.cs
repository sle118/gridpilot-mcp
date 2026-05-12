using ExcelMcp.Deployment.AgentConfig;
using ExcelMcp.Deployment.Installation;

namespace GridPilot.Tray;

internal static class VsCodeConfigWritePresenter
{
    public static bool CanWrite(AgentTarget target, InstalledInstanceState? install) =>
        target == AgentTarget.VsCodeCopilot && install is not null;

    public static string? GetAvailabilityNote(AgentTarget target, InstalledInstanceState? install, string configPath)
    {
        if (target != AgentTarget.VsCodeCopilot)
        {
            return null;
        }

        return install is null
            ? "Tray action unavailable: writing the VS Code user MCP config requires running the installed GridPilot tray."
            : $"Tray action available: use \"Write VS Code User Config...\" to preview and update {configPath}.";
    }

    public static string FormatDetails(VsCodeMcpConfigWriteResult result)
    {
        var lines = new List<string>();
        lines.AddRange(result.SummaryLines);
        if (result.Issues.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Issues:");
            lines.AddRange(result.Issues.Select(issue => $"[{issue.Severity}] {issue.Code}: {issue.Message}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string CreateLastActionMessage(VsCodeMcpConfigWriteResult result) =>
        result.Action switch
        {
            VsCodeMcpConfigWriteAction.Create when result.WasWritten =>
                $"VS Code user MCP config created at {result.ConfigPath}.",
            VsCodeMcpConfigWriteAction.Update when result.WasWritten && !string.IsNullOrWhiteSpace(result.BackupPath) =>
                $"VS Code user MCP config updated at {result.ConfigPath} with backup {result.BackupPath}.",
            VsCodeMcpConfigWriteAction.Update when result.WasWritten =>
                $"VS Code user MCP config updated at {result.ConfigPath}.",
            VsCodeMcpConfigWriteAction.NoChange =>
                "VS Code user MCP config already matches the installed GridPilot defaults.",
            VsCodeMcpConfigWriteAction.Failed =>
                "VS Code user MCP config preview or write failed.",
            _ =>
                "VS Code user MCP config action completed."
        };
}
