using ExcelMcp.Deployment.AgentConfig;
using ExcelMcp.Deployment.Installation;
using Xunit;

namespace GridPilot.Tray.Tests;

public sealed class VsCodeConfigWritePresenterTests
{
    [Fact]
    public void CanWrite_ReturnsTrueOnlyForVsCodeWithInstalledInstance()
    {
        var install = CreateInstalledInstance();

        Assert.True(VsCodeConfigWritePresenter.CanWrite(AgentTarget.VsCodeCopilot, install));
        Assert.False(VsCodeConfigWritePresenter.CanWrite(AgentTarget.CodexCli, install));
        Assert.False(VsCodeConfigWritePresenter.CanWrite(AgentTarget.VsCodeCopilot, null));
    }

    [Fact]
    public void GetAvailabilityNote_ExplainsInstalledActionForVsCode()
    {
        var install = CreateInstalledInstance();
        const string configPath = @"C:\Users\sle11\AppData\Roaming\Code\User\mcp.json";

        var available = VsCodeConfigWritePresenter.GetAvailabilityNote(AgentTarget.VsCodeCopilot, install, configPath);
        var unavailable = VsCodeConfigWritePresenter.GetAvailabilityNote(AgentTarget.VsCodeCopilot, null, configPath);
        var nonVsCode = VsCodeConfigWritePresenter.GetAvailabilityNote(AgentTarget.CodexCli, install, configPath);

        Assert.Contains(configPath, available, StringComparison.Ordinal);
        Assert.Contains("installed GridPilot tray", unavailable, StringComparison.Ordinal);
        Assert.Null(nonVsCode);
    }

    [Fact]
    public void CreateLastActionMessage_UsesResultShape()
    {
        var updated = new VsCodeMcpConfigWriteResult(
            @"C:\Users\sle11\AppData\Roaming\Code\User\mcp.json",
            @"C:\Users\sle11\AppData\Roaming\Code\User\mcp.json.20260512-120000Z.bak",
            VsCodeMcpConfigWriteAction.Update,
            WasWritten: true,
            Diff: "diff",
            SummaryLines: ["updated"],
            Issues: []);
        var noChange = updated with
        {
            Action = VsCodeMcpConfigWriteAction.NoChange,
            WasWritten = false,
            BackupPath = null,
            Diff = string.Empty
        };
        var failed = updated with
        {
            Action = VsCodeMcpConfigWriteAction.Failed,
            WasWritten = false,
            BackupPath = null
        };

        Assert.Contains("backup", VsCodeConfigWritePresenter.CreateLastActionMessage(updated), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already matches", VsCodeConfigWritePresenter.CreateLastActionMessage(noChange), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("failed", VsCodeConfigWritePresenter.CreateLastActionMessage(failed), StringComparison.OrdinalIgnoreCase);
    }

    private static InstalledInstanceState CreateInstalledInstance() =>
        new(
            InstallScope.PerUser,
            "v1.2.3",
            new InstallationPaths(
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\app",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\profiles",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\logs",
                @"C:\Users\sle11\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\GridPilot MCP",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\app\GridPilot.Tray.exe",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\app\GridPilot.Setup.exe",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\app\host\ExcelMcp.ToolHost.exe",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\app\proxy\ExcelMcp.ToolProxy.exe",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\profiles\gridpilot-default.json",
                @"C:\Users\sle11\AppData\Local\GridPilot MCP\install-state.json"),
            StartupEnabled: false,
            InstalledAtUtc: DateTimeOffset.UtcNow);
}
