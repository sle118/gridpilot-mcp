using System.Drawing.Drawing2D;

namespace GridPilot.Setup;

internal static class SetupBranding
{
    private static readonly Lazy<Icon> _appIcon = new(CreateIcon);
    private static readonly Lazy<Image> _heroImage = new(CreateHeroImage);

    public static Icon AppIcon => _appIcon.Value;

    public static Image HeroImage => _heroImage.Value;

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var backgroundBrush = new LinearGradientBrush(
            new Rectangle(0, 0, 64, 64),
            Color.FromArgb(12, 73, 131),
            Color.FromArgb(18, 147, 154),
            LinearGradientMode.ForwardDiagonal);
        graphics.FillRoundedRectangle(backgroundBrush, 4, 4, 56, 56, 12);

        using var gridPen = new Pen(Color.FromArgb(240, 247, 250), 4)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(gridPen, 18, 22, 46, 22);
        graphics.DrawLine(gridPen, 18, 32, 46, 32);
        graphics.DrawLine(gridPen, 18, 42, 34, 42);
        graphics.DrawLine(gridPen, 18, 18, 18, 46);
        graphics.DrawLine(gridPen, 32, 18, 32, 46);

        using var pilotBrush = new SolidBrush(Color.FromArgb(249, 192, 87));
        graphics.FillEllipse(pilotBrush, 36, 36, 12, 12);

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    private static Image CreateHeroImage()
    {
        var bitmap = new Bitmap(640, 180);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(245, 249, 251));

        using var backgroundBrush = new LinearGradientBrush(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            Color.FromArgb(229, 240, 245),
            Color.FromArgb(214, 236, 233),
            LinearGradientMode.Horizontal);
        graphics.FillRectangle(backgroundBrush, 0, 0, bitmap.Width, bitmap.Height);

        using var circleBrush = new SolidBrush(Color.FromArgb(40, 18, 147, 154));
        graphics.FillEllipse(circleBrush, 430, -20, 180, 180);
        graphics.FillEllipse(circleBrush, 500, 80, 120, 120);

        using var titleBrush = new SolidBrush(Color.FromArgb(14, 40, 62));
        using var titleFont = new Font("Segoe UI Semibold", 22, FontStyle.Bold, GraphicsUnit.Point);
        using var bodyFont = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        graphics.DrawString("GridPilot MCP Setup", titleFont, titleBrush, new PointF(28, 30));
        graphics.DrawString(
            "Install the tray shell and Excel bridge into a stable Windows layout, choose per-user or machine-wide scope, and optionally register quiet startup for the tray.",
            bodyFont,
            titleBrush,
            new RectangleF(30, 74, 360, 70));

        using var accentBrush = new SolidBrush(Color.FromArgb(249, 192, 87));
        graphics.FillEllipse(accentBrush, 392, 124, 16, 16);
        return bitmap;
    }
}
