using ExcelMcp.Deployment.AgentConfig;
using ExcelMcp.Deployment.Profiles;
using Xunit;

namespace GridPilot.Tray.Tests;

public sealed class AgentConfigPresenterTests
{
    [Fact]
    public void Targets_ExposeExpectedDisplayNames()
    {
        Assert.Contains(AgentConfigPresenter.Targets, target => target.Target == AgentTarget.VsCodeCopilot && target.DisplayName == "VS Code / Copilot");
        Assert.Contains(AgentConfigPresenter.Targets, target => target.Target == AgentTarget.CodexCli && target.DisplayName == "Codex CLI");
        Assert.Contains(AgentConfigPresenter.Targets, target => target.Target == AgentTarget.ClaudeCode && target.DisplayName == "Claude Code");
        Assert.Contains(AgentConfigPresenter.Targets, target => target.Target == AgentTarget.GenericMcpJson && target.DisplayName == "Generic MCP");
    }

    [Fact]
    public void CreatePreview_ValidProfileReturnsCopyableContent()
    {
        using var workspace = TrayProfileTestWorkspace.Create();
        var overview = ProfileOverviewPresenter.Create(new TrayProfileContext(workspace.WriteProfile()));

        var preview = AgentConfigPresenter.CreatePreview(overview.Profile, AgentTarget.CodexCli);

        Assert.True(preview.CanCopy);
        Assert.Contains("[mcp_servers.gridpilot-default]", preview.Content, StringComparison.Ordinal);
        Assert.Contains("GridPilotHost.exe", preview.Content, StringComparison.Ordinal);
        Assert.Equal("No issues.", preview.IssuesText);
    }

    [Fact]
    public void CreatePreview_WorkingDirectoryWarningAppearsForVsCode()
    {
        using var workspace = TrayProfileTestWorkspace.Create();
        var overview = ProfileOverviewPresenter.Create(new TrayProfileContext(workspace.WriteProfile()));

        var preview = AgentConfigPresenter.CreatePreview(overview.Profile, AgentTarget.VsCodeCopilot);

        Assert.True(preview.CanCopy);
        Assert.Contains("[Warning]", preview.IssuesText, StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePreview_InvalidProfileReturnsIssuesAndNoContent()
    {
        var profile = new LaunchProfile
        {
            SchemaVersion = 1,
            Name = "gridpilot-default",
            DisplayName = "GridPilot MCP",
            Host = new LaunchProfileHost
            {
                Command = @"C:\missing\GridPilotHost.exe",
                Args = [],
                Env = new Dictionary<string, string?>()
            },
            Logs = new LaunchProfileLogs
            {
                StdoutPolicy = "jsonRpcOnly"
            }
        };

        var preview = AgentConfigPresenter.CreatePreview(profile, AgentTarget.GenericMcpJson);

        Assert.False(preview.CanCopy);
        Assert.Empty(preview.Content);
        Assert.Contains("[Error]", preview.IssuesText, StringComparison.Ordinal);
    }
}
