namespace GridPilot.Tray;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About GridPilot MCP";
        Icon = TrayBranding.TrayIcon;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(560, 300);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var logo = new PictureBox
        {
            Image = TrayBranding.AboutImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(112, 112),
            Margin = new Padding(0, 0, 16, 0)
        };
        layout.Controls.Add(logo, 0, 0);
        layout.SetRowSpan(logo, 4);

        layout.Controls.Add(new Label
        {
            Text = "GridPilot MCP",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold)
        }, 1, 0);
        var summaryLabel = new Label
        {
            Text = "Optional Windows tray shell for local Excel MCP deployment diagnostics and launch support.",
            AutoSize = true,
            MaximumSize = new Size(380, 0)
        };
        var detailsLabel = new Label
        {
            Text = "The tray shell calls ExcelMcp.Deployment services and does not own MCP or Excel automation logic.",
            AutoSize = true,
            MaximumSize = new Size(380, 0)
        };

        layout.Controls.Add(summaryLabel, 1, 1);
        layout.Controls.Add(detailsLabel, 1, 2);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right,
            AutoSize = true
        };
        layout.Controls.Add(okButton, 1, 3);

        AcceptButton = okButton;
        Controls.Add(layout);
    }
}
