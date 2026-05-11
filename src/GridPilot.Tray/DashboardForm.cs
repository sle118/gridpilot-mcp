using System.Diagnostics;
using ExcelMcp.Deployment.AgentConfig;
using ExcelMcp.Deployment.Diagnostics;
using ExcelMcp.Deployment.Doctor;
using ExcelMcp.Deployment.Logs;
using ExcelMcp.Deployment.SmokeTests;

namespace GridPilot.Tray;

internal sealed class DashboardForm : Form
{
    private readonly TrayProfileContext _profileContext;
    private ProfileOverviewState _overviewState;
    private string _doctorSummary = string.Empty;
    private string _smokeSummary = string.Empty;
    private string _tailSummary = string.Empty;

    private readonly Label _profilePathLabel;
    private readonly Label _profileStatusLabel;
    private readonly TextBox _profileDetailsTextBox;
    private readonly TextBox _lastActionTextBox;
    private readonly ComboBox _agentTargetComboBox;
    private readonly Label _agentMetadataLabel;
    private readonly TextBox _agentPreviewTextBox;
    private readonly TextBox _agentIssuesTextBox;
    private readonly Button _copyAgentButton;
    private readonly Button _runDoctorButton;
    private readonly Button _copyDoctorButton;
    private readonly TextBox _doctorResultsTextBox;
    private readonly Button _runSmokeButton;
    private readonly Button _copySmokeButton;
    private readonly TextBox _smokeResultsTextBox;
    private readonly ListBox _logListBox;
    private readonly TextBox _logMetadataTextBox;
    private readonly TextBox _logTailTextBox;
    private readonly Button _openLogFolderButton;
    private readonly Button _readLogTailButton;
    private readonly Button _copyLogTailButton;

    public DashboardForm(TrayProfileContext profileContext)
    {
        _profileContext = profileContext;
        _overviewState = ProfileOverviewPresenter.Create(_profileContext);

        Text = "GridPilot MCP Dashboard";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 980;
        Height = 720;
        MinimizeBox = true;
        MaximizeBox = true;

        var tabs = new TabControl { Dock = DockStyle.Fill };

        (_profilePathLabel, _profileStatusLabel, _profileDetailsTextBox, _lastActionTextBox) = CreateOverviewTab(tabs);
        (_agentTargetComboBox, _agentMetadataLabel, _agentPreviewTextBox, _agentIssuesTextBox, _copyAgentButton) = CreateAgentsTab(tabs);
        (_runDoctorButton, _copyDoctorButton, _doctorResultsTextBox) = CreateDoctorTab(tabs);
        (_runSmokeButton, _copySmokeButton, _smokeResultsTextBox) = CreateSmokeTab(tabs);
        (_logListBox, _logMetadataTextBox, _logTailTextBox, _openLogFolderButton, _readLogTailButton, _copyLogTailButton) = CreateLogsTab(tabs);

        Controls.Add(tabs);
        RefreshProfileState();
    }

    public void RefreshProfileState()
    {
        _overviewState = ProfileOverviewPresenter.Create(_profileContext);
        _profilePathLabel.Text = string.IsNullOrWhiteSpace(_overviewState.ProfilePath)
            ? "Profile: (not configured)"
            : $"Profile: {_overviewState.ProfilePath}";
        _profileStatusLabel.Text = $"Status: {_overviewState.Status}";
        _profileDetailsTextBox.Text = _overviewState.Details;

        _agentTargetComboBox.Enabled = _overviewState.CanRunProfileActions;
        _copyAgentButton.Enabled = false;
        _runDoctorButton.Enabled = _overviewState.CanRunProfileActions;
        _runSmokeButton.Enabled = _overviewState.CanRunProfileActions;
        _openLogFolderButton.Enabled = _overviewState.CanRunProfileActions;
        _readLogTailButton.Enabled = _overviewState.CanRunProfileActions;
        _copyLogTailButton.Enabled = false;

        RefreshAgentPreview();
        RefreshLogCandidates();
    }

    public async Task CopyAgentConfigAsync(AgentTarget target)
    {
        SelectAgentTarget(target);
        RefreshAgentPreview();
        if (!_copyAgentButton.Enabled || string.IsNullOrEmpty(_agentPreviewTextBox.Text))
        {
            SetLastAction("No valid agent config is available to copy.");
            return;
        }

        Clipboard.SetText(_agentPreviewTextBox.Text);
        SetLastAction($"{AgentConfigPresenter.GetDisplayName(target)} MCP config copied to clipboard.");
        await Task.CompletedTask;
    }

    public async Task RunDoctorAsync()
    {
        if (!EnsureProfileAction("Doctor"))
        {
            return;
        }

        await RunWithButtonAsync(_runDoctorButton, async () =>
        {
            _doctorResultsTextBox.Text = "Doctor running...";
            var report = await new DoctorRunner().RunAsync(_profileContext.ProfilePath!);
            _doctorSummary = TrayResultFormatter.FormatDoctor(report);
            _doctorResultsTextBox.Text = _doctorSummary;
            _copyDoctorButton.Enabled = !string.IsNullOrWhiteSpace(_doctorSummary);
            SetLastAction("Doctor completed.");
        });
    }

    public async Task RunSmokeTestAsync()
    {
        if (!EnsureProfileAction("MCP smoke test"))
        {
            return;
        }

        await RunWithButtonAsync(_runSmokeButton, async () =>
        {
            _smokeResultsTextBox.Text = "MCP smoke test running...";
            var report = await new McpSmokeTestRunner().RunAsync(_profileContext.ProfilePath!);
            _smokeSummary = TrayResultFormatter.FormatSmoke(report);
            _smokeResultsTextBox.Text = _smokeSummary;
            _copySmokeButton.Enabled = !string.IsNullOrWhiteSpace(_smokeSummary);
            SetLastAction("MCP smoke test completed.");
        });
    }

    public void OpenLogsFolder()
    {
        RefreshLogCandidates();
        var selected = GetSelectedLog() ??
            _logListBox.Items.OfType<LogListItem>().FirstOrDefault(item => LogPresenter.GetExistingParentDirectory(item.Entry) is not null);
        if (selected is null)
        {
            SetLastAction("No log candidate is available for the active profile.");
            return;
        }

        var folder = LogPresenter.GetExistingParentDirectory(selected.Entry);
        if (folder is null)
        {
            SetLastAction("No existing log folder was found for the selected log candidate.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
        SetLastAction($"Opened log folder: {folder}");
    }

    public async Task CopyDiagnosticReportAsync()
    {
        if (!_overviewState.CanRunProfileActions || _overviewState.Profile is null)
        {
            SetLastAction(_overviewState.Details);
            return;
        }

        var report = await DeploymentDiagnosticReportBuilder.BuildAsync(_overviewState.Profile);
        Clipboard.SetText(report.Content);
        SetLastAction("Diagnostic report copied to clipboard.");
    }

    public void SetLastAction(string result)
    {
        _lastActionTextBox.Text = result;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private static (Label ProfilePathLabel, Label StatusLabel, TextBox DetailsTextBox, TextBox LastActionTextBox) CreateOverviewTab(TabControl tabs)
    {
        var tab = new TabPage("Overview");
        var layout = CreateVerticalLayout();

        var profilePathLabel = new Label { AutoSize = true };
        var statusLabel = new Label { AutoSize = true };
        var detailsTextBox = CreateReadOnlyTextBox();
        var lastActionTextBox = CreateReadOnlyTextBox(height: 90);
        var refreshButton = new Button { Text = "Refresh", AutoSize = true, Anchor = AnchorStyles.Right };

        layout.Controls.Add(profilePathLabel, 0, 0);
        layout.Controls.Add(statusLabel, 0, 1);
        layout.Controls.Add(Header("Profile details"), 0, 2);
        layout.Controls.Add(detailsTextBox, 0, 3);
        layout.Controls.Add(Header("Last action"), 0, 4);
        layout.Controls.Add(lastActionTextBox, 0, 5);
        layout.Controls.Add(refreshButton, 0, 6);

        tab.Controls.Add(layout);
        tabs.TabPages.Add(tab);

        refreshButton.Click += (sender, _) => ((DashboardForm)((Control)sender!).FindForm()!).RefreshProfileState();
        return (profilePathLabel, statusLabel, detailsTextBox, lastActionTextBox);
    }

    private static (ComboBox TargetComboBox, Label MetadataLabel, TextBox PreviewTextBox, TextBox IssuesTextBox, Button CopyButton) CreateAgentsTab(TabControl tabs)
    {
        var tab = new TabPage("Agents");
        var layout = CreateVerticalLayout();

        var targetComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = AgentConfigPresenter.Targets.ToList(),
            Dock = DockStyle.Top
        };
        var metadataLabel = new Label { AutoSize = true };
        var previewTextBox = CreateReadOnlyTextBox();
        var issuesTextBox = CreateReadOnlyTextBox(height: 90);
        var copyButton = new Button { Text = "Copy Preview", AutoSize = true, Anchor = AnchorStyles.Right };

        layout.Controls.Add(targetComboBox, 0, 0);
        layout.Controls.Add(metadataLabel, 0, 1);
        layout.Controls.Add(Header("Preview"), 0, 2);
        layout.Controls.Add(previewTextBox, 0, 3);
        layout.Controls.Add(Header("Issues"), 0, 4);
        layout.Controls.Add(issuesTextBox, 0, 5);
        layout.Controls.Add(copyButton, 0, 6);

        tab.Controls.Add(layout);
        tabs.TabPages.Add(tab);
        return (targetComboBox, metadataLabel, previewTextBox, issuesTextBox, copyButton);
    }

    private static (Button RunButton, Button CopyButton, TextBox ResultsTextBox) CreateDoctorTab(TabControl tabs)
    {
        var tab = new TabPage("Doctor");
        var layout = CreateVerticalLayout();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var runButton = new Button { Text = "Run Doctor", AutoSize = true };
        var copyButton = new Button { Text = "Copy Results", AutoSize = true, Enabled = false };
        var resultsTextBox = CreateReadOnlyTextBox();

        buttons.Controls.Add(runButton);
        buttons.Controls.Add(copyButton);
        layout.Controls.Add(buttons, 0, 0);
        layout.Controls.Add(resultsTextBox, 0, 1);

        tab.Controls.Add(layout);
        tabs.TabPages.Add(tab);
        return (runButton, copyButton, resultsTextBox);
    }

    private static (Button RunButton, Button CopyButton, TextBox ResultsTextBox) CreateSmokeTab(TabControl tabs)
    {
        var tab = new TabPage("Smoke Test");
        var layout = CreateVerticalLayout();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var runButton = new Button { Text = "Run Smoke Test", AutoSize = true };
        var copyButton = new Button { Text = "Copy Results", AutoSize = true, Enabled = false };
        var resultsTextBox = CreateReadOnlyTextBox();

        buttons.Controls.Add(runButton);
        buttons.Controls.Add(copyButton);
        layout.Controls.Add(buttons, 0, 0);
        layout.Controls.Add(resultsTextBox, 0, 1);

        tab.Controls.Add(layout);
        tabs.TabPages.Add(tab);
        return (runButton, copyButton, resultsTextBox);
    }

    private static (ListBox LogListBox, TextBox MetadataTextBox, TextBox TailTextBox, Button OpenFolderButton, Button ReadTailButton, Button CopyTailButton) CreateLogsTab(TabControl tabs)
    {
        var tab = new TabPage("Logs");
        var layout = CreateVerticalLayout();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var refreshButton = new Button { Text = "Refresh", AutoSize = true };
        var openFolderButton = new Button { Text = "Open Folder", AutoSize = true };
        var readTailButton = new Button { Text = "Read Recent Tail", AutoSize = true };
        var copyTailButton = new Button { Text = "Copy Tail", AutoSize = true, Enabled = false };
        var logListBox = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        var metadataTextBox = CreateReadOnlyTextBox(height: 120);
        var tailTextBox = CreateReadOnlyTextBox();

        buttons.Controls.Add(refreshButton);
        buttons.Controls.Add(openFolderButton);
        buttons.Controls.Add(readTailButton);
        buttons.Controls.Add(copyTailButton);
        layout.Controls.Add(buttons, 0, 0);
        layout.Controls.Add(logListBox, 0, 1);
        layout.Controls.Add(Header("Metadata"), 0, 2);
        layout.Controls.Add(metadataTextBox, 0, 3);
        layout.Controls.Add(Header("Recent tail"), 0, 4);
        layout.Controls.Add(tailTextBox, 0, 5);

        tab.Controls.Add(layout);
        tabs.TabPages.Add(tab);

        refreshButton.Click += (sender, _) => ((DashboardForm)((Control)sender!).FindForm()!).RefreshLogCandidates();
        return (logListBox, metadataTextBox, tailTextBox, openFolderButton, readTailButton, copyTailButton);
    }

    private void WireEvents()
    {
        _agentTargetComboBox.SelectedIndexChanged += (_, _) => RefreshAgentPreview();
        _copyAgentButton.Click += async (_, _) => await CopySelectedAgentConfigAsync();
        _runDoctorButton.Click += async (_, _) => await RunDoctorAsync();
        _copyDoctorButton.Click += (_, _) => CopyText(_doctorSummary, "Doctor results copied to clipboard.");
        _runSmokeButton.Click += async (_, _) => await RunSmokeTestAsync();
        _copySmokeButton.Click += (_, _) => CopyText(_smokeSummary, "Smoke-test results copied to clipboard.");
        _logListBox.SelectedIndexChanged += (_, _) => RefreshSelectedLogMetadata();
        _openLogFolderButton.Click += (_, _) => OpenLogsFolder();
        _readLogTailButton.Click += async (_, _) => await ReadSelectedLogTailAsync();
        _copyLogTailButton.Click += (_, _) => CopyText(_tailSummary, "Log tail copied to clipboard.");
    }

    private void RefreshAgentPreview()
    {
        var target = GetSelectedAgentTarget();
        var state = AgentConfigPresenter.CreatePreview(
            _overviewState.CanRunProfileActions ? _overviewState.Profile : null,
            target);
        _agentMetadataLabel.Text = string.IsNullOrWhiteSpace(state.SuggestedFileName)
            ? state.DisplayName
            : $"{state.DisplayName} ({state.SuggestedFileName}, {state.Language})";
        _agentPreviewTextBox.Text = state.Content;
        _agentIssuesTextBox.Text = state.IssuesText;
        _copyAgentButton.Enabled = _overviewState.CanRunProfileActions && state.CanCopy;
    }

    private async Task CopySelectedAgentConfigAsync()
    {
        await CopyAgentConfigAsync(GetSelectedAgentTarget());
    }

    private AgentTarget GetSelectedAgentTarget() =>
        _agentTargetComboBox.SelectedItem is AgentTargetItem item ? item.Target : AgentTarget.VsCodeCopilot;

    private void SelectAgentTarget(AgentTarget target)
    {
        for (var index = 0; index < _agentTargetComboBox.Items.Count; index++)
        {
            if (_agentTargetComboBox.Items[index] is AgentTargetItem item && item.Target == target)
            {
                _agentTargetComboBox.SelectedIndex = index;
                return;
            }
        }
    }

    private void RefreshLogCandidates()
    {
        _logListBox.Items.Clear();
        _logMetadataTextBox.Clear();
        _logTailTextBox.Clear();
        _tailSummary = string.Empty;
        _copyLogTailButton.Enabled = false;

        if (!_overviewState.CanRunProfileActions || _overviewState.Profile is null)
        {
            _logMetadataTextBox.Text = _overviewState.Details;
            return;
        }

        foreach (var log in DeploymentLogLocator.Locate(_overviewState.Profile))
        {
            _logListBox.Items.Add(new LogListItem(log));
        }

        if (_logListBox.Items.Count > 0)
        {
            _logListBox.SelectedIndex = 0;
        }
    }

    private void RefreshSelectedLogMetadata()
    {
        var selected = GetSelectedLog();
        _logMetadataTextBox.Text = selected is null ? "No log candidate selected." : LogPresenter.FormatLogMetadata(selected.Entry);
    }

    private async Task ReadSelectedLogTailAsync()
    {
        var selected = GetSelectedLog();
        if (selected is null)
        {
            SetLastAction("No log candidate selected.");
            return;
        }

        await RunWithButtonAsync(_readLogTailButton, async () =>
        {
            _logTailTextBox.Text = "Reading recent log tail...";
            var tail = await RecentLogReader.ReadTailAsync(selected.Entry.Path);
            _tailSummary = LogPresenter.FormatTail(tail);
            _logTailTextBox.Text = _tailSummary;
            _copyLogTailButton.Enabled = !string.IsNullOrWhiteSpace(_tailSummary);
            SetLastAction("Recent log tail loaded.");
        });
    }

    private LogListItem? GetSelectedLog() =>
        _logListBox.SelectedItem as LogListItem;

    private bool EnsureProfileAction(string actionName)
    {
        RefreshProfileState();
        if (_overviewState.CanRunProfileActions)
        {
            return true;
        }

        SetLastAction($"{actionName} cannot run: {_overviewState.Details}");
        return false;
    }

    private async Task RunWithButtonAsync(Button button, Func<Task> action)
    {
        button.Enabled = false;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            SetLastAction($"Action failed: {exception.Message}");
        }
        finally
        {
            button.Enabled = _overviewState.CanRunProfileActions;
        }
    }

    private void CopyText(string text, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetLastAction("No text is available to copy.");
            return;
        }

        Clipboard.SetText(text);
        SetLastAction(successMessage);
    }

    private static TableLayoutPanel CreateVerticalLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private static TextBox CreateReadOnlyTextBox(int? height = null)
    {
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false
        };
        if (height is not null)
        {
            textBox.Height = height.Value;
            textBox.MinimumSize = new Size(0, height.Value);
            textBox.Dock = DockStyle.Top;
        }

        return textBox;
    }

    private static Label Header(string text) =>
        new() { Text = text, AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        WireEvents();
    }

    private sealed record LogListItem(DeploymentLogEntry Entry)
    {
        public override string ToString() => $"{Entry.Kind}: {Entry.Path}";
    }
}
