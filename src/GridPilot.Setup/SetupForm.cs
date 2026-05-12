using System.Diagnostics;
using ExcelMcp.Deployment.Installation;

namespace GridPilot.Setup;

internal sealed class SetupForm : Form
{
    private readonly InstallationService _installationService;
    private readonly ReleasePayloadInfo? _payload;
    private readonly SetupResumeState? _resumeState;

    private readonly TabControl _wizardTabs;
    private readonly TextBox _detectedStateTextBox;
    private readonly RadioButton _perUserRadioButton;
    private readonly RadioButton _machineWideRadioButton;
    private readonly Label _destinationLabel;
    private readonly Label _elevationLabel;
    private readonly CheckBox _startupCheckBox;
    private readonly CheckBox _shortcutCheckBox;
    private readonly TextBox _previewTextBox;
    private readonly Label _operationLabel;
    private readonly Button _installButton;
    private readonly Button _uninstallButton;
    private readonly TextBox _executionTextBox;
    private readonly Label _finishSummaryLabel;
    private readonly Button _launchTrayButton;
    private readonly Button _openInstallFolderButton;
    private readonly Button _backButton;
    private readonly Button _nextButton;

    private InstalledInstanceState? _lastInstalledState;
    private SetupPlan? _currentPlan;
    private bool _isExecuting;

    public SetupForm(SetupResumeState? resumeState)
    {
        _installationService = new InstallationService();
        _resumeState = resumeState;

        Text = "GridPilot MCP Setup";
        Width = 880;
        Height = 700;
        MinimumSize = new Size(860, 680);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SetupBranding.AppIcon;

        _payload = TryReadPayload();

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        rootLayout.Controls.Add(CreateHeader(), 0, 0);

        _wizardTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.FlatButtons,
            ItemSize = new Size(0, 1),
            SizeMode = TabSizeMode.Fixed
        };

        _detectedStateTextBox = CreateReadOnlyTextBox();
        _perUserRadioButton = new RadioButton { Text = "Per-user install", Checked = true, AutoSize = true };
        _machineWideRadioButton = new RadioButton { Text = "Machine-wide install", AutoSize = true };
        _destinationLabel = new Label { AutoSize = true };
        _elevationLabel = new Label { AutoSize = true, ForeColor = Color.FromArgb(124, 67, 0) };
        _startupCheckBox = new CheckBox { Text = "Launch GridPilot Tray at Windows sign-in", AutoSize = true, Checked = true };
        _shortcutCheckBox = new CheckBox { Text = "Create Start Menu shortcut", AutoSize = true, Checked = true };
        _previewTextBox = CreateReadOnlyTextBox();
        _operationLabel = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point) };
        _installButton = new Button { Text = "Install", AutoSize = true };
        _uninstallButton = new Button { Text = "Uninstall", AutoSize = true };
        _executionTextBox = CreateReadOnlyTextBox();
        _finishSummaryLabel = new Label { AutoSize = true, MaximumSize = new Size(760, 0) };
        _launchTrayButton = new Button { Text = "Launch GridPilot Tray", AutoSize = true, Enabled = false };
        _openInstallFolderButton = new Button { Text = "Open install folder", AutoSize = true, Enabled = false };
        _backButton = new Button { Text = "Back", AutoSize = true };
        _nextButton = new Button { Text = "Next", AutoSize = true };

        _wizardTabs.TabPages.Add(CreateDetectPage());
        _wizardTabs.TabPages.Add(CreateScopePage());
        _wizardTabs.TabPages.Add(CreateStartupPage());
        _wizardTabs.TabPages.Add(CreatePreviewPage());
        _wizardTabs.TabPages.Add(CreateExecutePage());
        _wizardTabs.TabPages.Add(CreateFinishPage());
        rootLayout.Controls.Add(_wizardTabs, 0, 1);
        rootLayout.Controls.Add(CreateFooter(), 0, 2);
        Controls.Add(rootLayout);

        _backButton.Click += (_, _) => MovePage(-1);
        _nextButton.Click += (_, _) => MovePage(1);
        _perUserRadioButton.CheckedChanged += (_, _) => RefreshScopeDetails();
        _machineWideRadioButton.CheckedChanged += (_, _) => RefreshScopeDetails();
        _startupCheckBox.CheckedChanged += (_, _) => RefreshPreview();
        _shortcutCheckBox.CheckedChanged += (_, _) => RefreshPreview();
        _installButton.Click += async (_, _) => await ExecuteSelectedInstallOperationAsync();
        _uninstallButton.Click += async (_, _) => await ExecuteUninstallAsync();
        _launchTrayButton.Click += (_, _) => LaunchInstalledTray();
        _openInstallFolderButton.Click += (_, _) => OpenInstallFolder();

        RefreshDetectedState();
        RefreshScopeDetails();
        ApplyResumeState();
        UpdateNavigationState();
    }

    private static TextBox CreateReadOnlyTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

    private Control CreateHeader()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 180, Padding = new Padding(0, 0, 0, 16) };
        var picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = SetupBranding.HeroImage,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        panel.Controls.Add(picture);
        return panel;
    }

    private TabPage CreateDetectPage()
    {
        var tab = new TabPage("Detect");
        var layout = CreatePageLayout(stretchRowIndex: 3);
        layout.Controls.Add(Header("Release payload"), 0, 0);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = _payload is null
                ? "This setup app must run from an extracted GridPilot MCP release folder that contains the release manifest, tray app, setup app, host, and proxy."
                : $"Setup is running from {_payload.SourceRoot} and detected release {_payload.Version}."
        }, 0, 1);
        layout.Controls.Add(Header("Existing installs"), 0, 2);
        layout.Controls.Add(_detectedStateTextBox, 0, 3);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage CreateScopePage()
    {
        var tab = new TabPage("Scope");
        var layout = CreatePageLayout();
        var radios = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        radios.Controls.Add(_perUserRadioButton);
        radios.Controls.Add(_machineWideRadioButton);

        layout.Controls.Add(Header("Choose install scope"), 0, 0);
        layout.Controls.Add(radios, 0, 1);
        layout.Controls.Add(_destinationLabel, 0, 2);
        layout.Controls.Add(_elevationLabel, 0, 3);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage CreateStartupPage()
    {
        var tab = new TabPage("Startup");
        var layout = CreatePageLayout();
        layout.Controls.Add(Header("Startup and shortcuts"), 0, 0);
        layout.Controls.Add(_startupCheckBox, 0, 1);
        layout.Controls.Add(_shortcutCheckBox, 0, 2);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Text = "Startup registration launches GridPilot.Tray.exe with --startup --no-dashboard. The MCP host remains on-demand."
        }, 0, 3);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage CreatePreviewPage()
    {
        var tab = new TabPage("Preview");
        var layout = CreatePageLayout(stretchRowIndex: 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(_installButton);
        buttons.Controls.Add(_uninstallButton);
        layout.Controls.Add(Header("Planned actions"), 0, 0);
        layout.Controls.Add(_operationLabel, 0, 1);
        layout.Controls.Add(_previewTextBox, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage CreateExecutePage()
    {
        var tab = new TabPage("Execute");
        var layout = CreatePageLayout(stretchRowIndex: 1);
        layout.Controls.Add(Header("Execution"), 0, 0);
        layout.Controls.Add(_executionTextBox, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage CreateFinishPage()
    {
        var tab = new TabPage("Finish");
        var layout = CreatePageLayout();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(_launchTrayButton);
        buttons.Controls.Add(_openInstallFolderButton);
        layout.Controls.Add(Header("Completed"), 0, 0);
        layout.Controls.Add(_finishSummaryLabel, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        tab.Controls.Add(layout);
        return tab;
    }

    private Control CreateFooter()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 12, 0, 0)
        };
        panel.Controls.Add(_nextButton);
        panel.Controls.Add(_backButton);
        return panel;
    }

    private static TableLayoutPanel CreatePageLayout(int? stretchRowIndex = null)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(18, 10, 18, 18)
        };
        for (var index = 0; index < layout.RowCount; index++)
        {
            layout.RowStyles.Add(new RowStyle(
                stretchRowIndex.HasValue && stretchRowIndex.Value == index ? SizeType.Percent : SizeType.AutoSize,
                stretchRowIndex.HasValue && stretchRowIndex.Value == index ? 100 : 0));
        }

        return layout;
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold, GraphicsUnit.Point),
        Margin = new Padding(0, 0, 0, 8)
    };

    private ReleasePayloadInfo? TryReadPayload()
    {
        try
        {
            return ReleasePayloadReader.Read(AppContext.BaseDirectory);
        }
        catch
        {
            return null;
        }
    }

    private void RefreshDetectedState()
    {
        var installs = _installationService.DiscoverAll();
        _detectedStateTextBox.Text = installs.Count == 0
            ? "No installed GridPilot MCP instances were detected."
            : string.Join(
                Environment.NewLine + Environment.NewLine,
                installs.Select(FormatInstallSummary));
        RefreshPreview();
    }

    private void RefreshScopeDetails()
    {
        var scope = SelectedScope;
        var paths = InstallationPathsResolver.Resolve(scope);
        _destinationLabel.Text = $"Install root: {paths.InstallRoot}";
        _elevationLabel.Text = scope == InstallScope.MachineWide
            ? "Machine-wide install writes to Program Files and will elevate if the current process is not running as administrator."
            : "Per-user install stays under LocalAppData and does not require administrator rights.";
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_payload is null)
        {
            _operationLabel.Text = "Payload not available";
            _previewTextBox.Text = "This setup app must run from an extracted release folder.";
            _installButton.Enabled = false;
            _uninstallButton.Enabled = false;
            return;
        }

        var isElevated = OperatingSystem.IsWindows() && SetupElevationPolicy.IsProcessElevated();
        _currentPlan = _installationService.BuildPlan(
            new SetupOptions(SelectedScope, _payload.SourceRoot, _startupCheckBox.Checked, _shortcutCheckBox.Checked),
            isElevated);
        _operationLabel.Text = $"Planned operation: {_currentPlan.Operation}";
        _previewTextBox.Text = string.Join(Environment.NewLine, _currentPlan.PreviewLines);
        _installButton.Text = _currentPlan.Operation switch
        {
            SetupOperationKind.Install => "Install",
            SetupOperationKind.Update => "Update",
            SetupOperationKind.Repair => "Repair",
            _ => "Apply"
        };
        _installButton.Enabled = true;
        _uninstallButton.Enabled = _installationService.Discover(SelectedScope) is not null;
    }

    private void ApplyResumeState()
    {
        if (_resumeState is null)
        {
            return;
        }

        if (_resumeState.Options.Scope == InstallScope.MachineWide)
        {
            _machineWideRadioButton.Checked = true;
        }
        else
        {
            _perUserRadioButton.Checked = true;
        }

        _startupCheckBox.Checked = _resumeState.Options.StartupEnabled;
        _shortcutCheckBox.Checked = _resumeState.Options.CreateStartMenuShortcut;

        Shown += async (_, _) =>
        {
            await ExecuteOperationAsync(_resumeState.Operation, fromResume: true);
        };
    }

    private InstallScope SelectedScope => _machineWideRadioButton.Checked ? InstallScope.MachineWide : InstallScope.PerUser;

    private void MovePage(int delta)
    {
        var nextIndex = Math.Clamp(_wizardTabs.SelectedIndex + delta, 0, _wizardTabs.TabPages.Count - 1);
        if (nextIndex == 3)
        {
            RefreshPreview();
        }

        _wizardTabs.SelectedIndex = nextIndex;
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        _backButton.Enabled = !_isExecuting && _wizardTabs.SelectedIndex > 0 && _wizardTabs.SelectedIndex < 4;
        _nextButton.Enabled = !_isExecuting && _wizardTabs.SelectedIndex < 3 && _payload is not null;
        _nextButton.Visible = _wizardTabs.SelectedIndex < 4;
        _backButton.Visible = _wizardTabs.SelectedIndex < 5;
    }

    private async Task ExecuteSelectedInstallOperationAsync()
    {
        if (_currentPlan is null)
        {
            return;
        }

        await ExecuteOperationAsync(_currentPlan.Operation, fromResume: false);
    }

    private async Task ExecuteUninstallAsync()
    {
        await ExecuteOperationAsync(SetupOperationKind.Uninstall, fromResume: false);
    }

    private async Task ExecuteOperationAsync(SetupOperationKind operation, bool fromResume)
    {
        if (_payload is null || _isExecuting)
        {
            return;
        }

        var isElevated = OperatingSystem.IsWindows() && SetupElevationPolicy.IsProcessElevated();
        var options = new SetupOptions(SelectedScope, _payload.SourceRoot, _startupCheckBox.Checked, _shortcutCheckBox.Checked);
        if (!fromResume && SetupElevationPolicy.RequiresElevation(SelectedScope, isElevated))
        {
            RelaunchElevated(new SetupResumeState(operation, options));
            return;
        }

        _isExecuting = true;
        _wizardTabs.SelectedIndex = 4;
        UpdateNavigationState();
        _executionTextBox.Text = string.Empty;
        AppendExecutionLine($"Starting {operation} for {SelectedScope} scope.");

        try
        {
            switch (operation)
            {
                case SetupOperationKind.Install:
                case SetupOperationKind.Update:
                case SetupOperationKind.Repair:
                    _currentPlan = _installationService.BuildPlan(options, isElevated);
                    _lastInstalledState = await _installationService.ApplyPlanAsync(_currentPlan);
                    AppendExecutionLine($"{operation} completed.");
                    AppendExecutionLine($"Installed version: {_lastInstalledState.Version}");
                    _finishSummaryLabel.Text = $"{operation} completed successfully. GridPilot MCP is installed at {_lastInstalledState.Paths.InstallRoot}.";
                    _launchTrayButton.Enabled = true;
                    _openInstallFolderButton.Enabled = true;
                    break;
                case SetupOperationKind.Uninstall:
                    var uninstallPreview = _installationService.BuildUninstallPreview(SelectedScope);
                    foreach (var line in uninstallPreview)
                    {
                        AppendExecutionLine(line);
                    }

                    await _installationService.UninstallAsync(SelectedScope);
                    AppendExecutionLine("Uninstall completed.");
                    _lastInstalledState = null;
                    _finishSummaryLabel.Text = $"{SelectedScope} install removed. User profiles and logs were preserved.";
                    _launchTrayButton.Enabled = false;
                    _openInstallFolderButton.Enabled = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            AppendExecutionLine($"Operation failed: {ex.Message}");
            _finishSummaryLabel.Text = $"The operation failed: {ex.Message}";
            _launchTrayButton.Enabled = false;
            _openInstallFolderButton.Enabled = false;
        }
        finally
        {
            _isExecuting = false;
            _wizardTabs.SelectedIndex = 5;
            UpdateNavigationState();
            RefreshDetectedState();
        }
    }

    private void RelaunchElevated(SetupResumeState state)
    {
        var executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
        var encodedState = SetupResumeCodec.Encode(state);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--resume {encodedState}",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(startInfo);
        Close();
    }

    private void AppendExecutionLine(string text)
    {
        _executionTextBox.AppendText(text + Environment.NewLine);
    }

    private void LaunchInstalledTray()
    {
        if (_lastInstalledState is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastInstalledState.Paths.TrayExecutablePath,
            Arguments = "--open-dashboard",
            WorkingDirectory = _lastInstalledState.Paths.InstallRoot,
            UseShellExecute = true
        });
    }

    private void OpenInstallFolder()
    {
        if (_lastInstalledState is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastInstalledState.Paths.InstallRoot,
            UseShellExecute = true
        });
    }

    private static string FormatInstallSummary(InstalledInstanceState state) =>
        $"{state.Scope}: version {state.Version}{Environment.NewLine}" +
        $"Install root: {state.Paths.InstallRoot}{Environment.NewLine}" +
        $"Startup: {(state.StartupEnabled ? "enabled" : "disabled")}";
}
