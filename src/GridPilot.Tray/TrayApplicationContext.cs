using ExcelMcp.Deployment.AgentConfig;

namespace GridPilot.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly TrayProfileContext _profileContext;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private DashboardForm? _dashboardForm;

    public TrayApplicationContext(TrayProfileContext profileContext)
    {
        _profileContext = profileContext;
        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "GridPilot MCP",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowDashboardWindow();
        RebuildMenu();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _dashboardForm?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RebuildMenu()
    {
        var status = _profileContext.GetStatus();
        _notifyIcon.Text = status.Message.Length > 63 ? status.Message[..63] : status.Message;
        _menu.Items.Clear();

        _menu.Items.Add(Disabled($"Status: {status.Message}"));
        _menu.Items.Add(Disabled(_profileContext.HasProfilePath ? $"Profile: {_profileContext.ProfilePath}" : "Profile: Not configured"));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Open dashboard", (_, _) => ShowDashboardWindow()));

        var copyMenu = new ToolStripMenuItem("Copy MCP config") { Enabled = status.CanRunProfileActions };
        copyMenu.DropDownItems.Add(Item("VS Code / Copilot", async (_, _) => await ShowDashboardWindow().CopyAgentConfigAsync(AgentTarget.VsCodeCopilot)));
        copyMenu.DropDownItems.Add(Item("Codex CLI", async (_, _) => await ShowDashboardWindow().CopyAgentConfigAsync(AgentTarget.CodexCli)));
        copyMenu.DropDownItems.Add(Item("Claude Code", async (_, _) => await ShowDashboardWindow().CopyAgentConfigAsync(AgentTarget.ClaudeCode)));
        copyMenu.DropDownItems.Add(Item("Generic MCP", async (_, _) => await ShowDashboardWindow().CopyAgentConfigAsync(AgentTarget.GenericMcpJson)));
        _menu.Items.Add(copyMenu);

        _menu.Items.Add(Item("Run doctor", async (_, _) => await ShowDashboardWindow().RunDoctorAsync(), status.CanRunProfileActions));
        _menu.Items.Add(Item("Run MCP smoke test", async (_, _) => await ShowDashboardWindow().RunSmokeTestAsync(), status.CanRunProfileActions));
        _menu.Items.Add(Item("Open logs folder", (_, _) => ShowDashboardWindow().OpenLogsFolder(), status.CanRunProfileActions));
        _menu.Items.Add(Item("Copy diagnostic report", async (_, _) => await ShowDashboardWindow().CopyDiagnosticReportAsync(), status.CanRunProfileActions));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("About", (_, _) => new AboutForm().ShowDialog()));
        _menu.Items.Add(Item("Exit", (_, _) => ExitThread()));
    }

    private static ToolStripMenuItem Item(string text, EventHandler onClick, bool enabled = true)
    {
        var item = new ToolStripMenuItem(text) { Enabled = enabled };
        item.Click += onClick;
        return item;
    }

    private static ToolStripMenuItem Disabled(string text) =>
        new(text) { Enabled = false };

    private DashboardForm ShowDashboardWindow()
    {
        _dashboardForm ??= new DashboardForm(_profileContext);
        _dashboardForm.RefreshProfileState();
        if (_dashboardForm.Visible)
        {
            _dashboardForm.Activate();
            return _dashboardForm;
        }

        _dashboardForm.Show();
        return _dashboardForm;
    }
}
