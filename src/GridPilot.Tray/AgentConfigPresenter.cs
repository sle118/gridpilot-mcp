using ExcelMcp.Deployment.AgentConfig;
using ExcelMcp.Deployment.Profiles;

namespace GridPilot.Tray;

internal static class AgentConfigPresenter
{
    public static IReadOnlyList<AgentTargetItem> Targets { get; } =
    [
        new(AgentTarget.VsCodeCopilot, "VS Code / Copilot"),
        new(AgentTarget.CodexCli, "Codex CLI"),
        new(AgentTarget.ClaudeCode, "Claude Code"),
        new(AgentTarget.GenericMcpJson, "Generic MCP")
    ];

    public static AgentConfigPreviewState CreatePreview(LaunchProfile? profile, AgentTarget target)
    {
        var displayName = GetDisplayName(target);
        if (profile is null)
        {
            return new AgentConfigPreviewState(
                displayName,
                SuggestedFileName: string.Empty,
                Language: string.Empty,
                Content: string.Empty,
                IssuesText: "No valid profile is loaded.",
                CanCopy: false);
        }

        var snippet = AgentConfigEmitter.Emit(profile, target);
        return new AgentConfigPreviewState(
            snippet.DisplayName,
            snippet.SuggestedFileName,
            snippet.Language,
            snippet.Content,
            FormatIssues(snippet.Issues),
            snippet.IsSuccess);
    }

    public static string GetDisplayName(AgentTarget target) =>
        Targets.First(item => item.Target == target).DisplayName;

    public static string FormatIssues(IEnumerable<AgentConfigIssue> issues)
    {
        var issueLines = issues
            .SelectMany(FormatIssueLines)
            .ToArray();
        return issueLines.Length == 0 ? "No issues." : string.Join(Environment.NewLine, issueLines);
    }

    private static IEnumerable<string> FormatIssueLines(AgentConfigIssue issue)
    {
        yield return $"[{issue.Severity}] {issue.Code}: {issue.Message}";

        switch (issue.Code)
        {
            case "vscode_cwd_not_emitted":
                yield return "Resolution: remove host.workingDirectory from the launch profile when command and log paths are already absolute, or use the Codex CLI / Generic MCP target which can emit cwd.";
                break;
            case "claude_cwd_not_emitted":
                yield return "Resolution: remove host.workingDirectory from the launch profile when command and log paths are already absolute, or use the Codex CLI / Generic MCP target which can emit cwd.";
                break;
        }
    }
}
