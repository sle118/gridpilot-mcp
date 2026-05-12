using ExcelMcp.Deployment.AgentConfig;

namespace GridPilot.Tray;

internal sealed class VsCodeConfigWritePreviewDialog : Form
{
    public VsCodeConfigWritePreviewDialog(VsCodeMcpConfigWriteResult previewResult)
    {
        ArgumentNullException.ThrowIfNull(previewResult);

        Text = "Write VS Code User MCP Config";
        StartPosition = FormStartPosition.CenterParent;
        Width = 900;
        Height = 700;
        MinimizeBox = false;
        MaximizeBox = true;
        Icon = TrayBranding.TrayIcon;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(12),
            RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var pathLabel = new Label
        {
            AutoSize = true,
            Text = $"Config path: {previewResult.ConfigPath}"
        };
        var actionLabel = new Label
        {
            AutoSize = true,
            Text = $"Preview action: {previewResult.Action}"
        };
        var summaryTextBox = CreateReadOnlyTextBox();
        summaryTextBox.Text = VsCodeConfigWritePresenter.FormatDetails(previewResult);
        var diffTextBox = CreateReadOnlyTextBox();
        diffTextBox.Text = string.IsNullOrWhiteSpace(previewResult.Diff)
            ? "(no diff)"
            : previewResult.Diff;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var closeButton = new Button
        {
            Text = previewResult.IsSuccess && previewResult.Action is VsCodeMcpConfigWriteAction.Create or VsCodeMcpConfigWriteAction.Update
                ? "Cancel"
                : "Close",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };
        buttons.Controls.Add(closeButton);

        if (previewResult.IsSuccess && previewResult.Action is VsCodeMcpConfigWriteAction.Create or VsCodeMcpConfigWriteAction.Update)
        {
            var writeButton = new Button
            {
                Text = "Write Config",
                AutoSize = true,
                DialogResult = DialogResult.OK
            };
            buttons.Controls.Add(writeButton);
            AcceptButton = writeButton;
        }

        CancelButton = closeButton;

        layout.Controls.Add(pathLabel, 0, 0);
        layout.Controls.Add(actionLabel, 0, 1);
        layout.Controls.Add(Header("Summary"), 0, 2);
        layout.Controls.Add(summaryTextBox, 0, 3);
        layout.Controls.Add(Header("Diff"), 0, 4);
        layout.Controls.Add(diffTextBox, 0, 5);
        layout.Controls.Add(buttons, 0, 6);

        Controls.Add(layout);
    }

    private static TextBox CreateReadOnlyTextBox() =>
        new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false
        };

    private static Label Header(string text) =>
        new() { Text = text, AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
}
