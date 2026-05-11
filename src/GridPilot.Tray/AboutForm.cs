namespace GridPilot.Tray;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About GridPilot MCP";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 420;
        Height = 220;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "GridPilot MCP",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold)
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "Optional Windows tray shell for local Excel MCP deployment diagnostics.",
            AutoSize = true
        }, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = "This shell calls ExcelMcp.Deployment services and does not own MCP or Excel automation logic.",
            AutoSize = true
        }, 0, 2);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right,
            AutoSize = true
        };
        layout.Controls.Add(okButton, 0, 3);

        AcceptButton = okButton;
        Controls.Add(layout);
    }
}
