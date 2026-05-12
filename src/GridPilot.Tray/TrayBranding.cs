using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GridPilot.Tray;

internal static class TrayBranding
{
    private static readonly Color DeepGreen = Color.FromArgb(0x0F, 0x6A, 0x4A);
    private static readonly Color BrightGreen = Color.FromArgb(0x14, 0xB8, 0x8A);
    private static readonly Color AccentCyan = Color.FromArgb(0x22, 0xD3, 0xEE);
    private static readonly Color DarkSlate = Color.FromArgb(0x1F, 0x29, 0x33);

    private static Icon? _trayIcon;
    private static Image? _aboutImage;

    public static Icon TrayIcon => _trayIcon ??= CreateIcon(32);

    public static Image AboutImage => _aboutImage ??= CreateBitmap(120);

    private static Icon CreateIcon(int size)
    {
        using var bitmap = CreateBitmap(size);
        var handle = bitmap.GetHicon();

        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Bitmap CreateBitmap(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var margin = size * 0.1f;
        var outer = new RectangleF(margin, margin, size - margin * 2, size - margin * 2);
        using (var fillBrush = new SolidBrush(Color.FromArgb(0x08, 0x16, 0x22)))
        using (var borderPen = new Pen(BrightGreen, Math.Max(2f, size * 0.08f)))
        {
            borderPen.LineJoin = LineJoin.Round;
            graphics.FillRoundedRectangle(fillBrush, outer, size * 0.18f);
            graphics.DrawRoundedRectangle(borderPen, outer, size * 0.18f);
        }

        var gridLeft = outer.Left + size * 0.08f;
        var gridTop = outer.Top + size * 0.08f;
        var gridWidth = size * 0.48f;
        var gridHeight = size * 0.48f;
        using (var gridPen = new Pen(DeepGreen, Math.Max(1.5f, size * 0.03f)))
        {
            gridPen.LineJoin = LineJoin.Round;
            for (var column = 1; column < 4; column++)
            {
                var x = gridLeft + gridWidth * column / 4f;
                graphics.DrawLine(gridPen, x, gridTop, x, gridTop + gridHeight);
            }

            for (var row = 1; row < 4; row++)
            {
                var y = gridTop + gridHeight * row / 4f;
                graphics.DrawLine(gridPen, gridLeft, y, gridLeft + gridWidth, y);
            }
        }

        using (var accentPen = new Pen(BrightGreen, Math.Max(2f, size * 0.055f)))
        {
            accentPen.StartCap = LineCap.Round;
            accentPen.EndCap = LineCap.Round;
            graphics.DrawBezier(
                accentPen,
                outer.Left + size * 0.12f,
                outer.Bottom - size * 0.18f,
                outer.Left + size * 0.28f,
                outer.Bottom - size * 0.24f,
                outer.Left + size * 0.46f,
                outer.Top + size * 0.30f,
                outer.Right - size * 0.12f,
                outer.Top + size * 0.12f);
        }

        var planePoints = new[]
        {
            new PointF(outer.Left + size * 0.26f, outer.Top + size * 0.42f),
            new PointF(outer.Left + size * 0.70f, outer.Top + size * 0.18f),
            new PointF(outer.Left + size * 0.58f, outer.Top + size * 0.54f),
            new PointF(outer.Left + size * 0.80f, outer.Top + size * 0.74f),
            new PointF(outer.Left + size * 0.50f, outer.Top + size * 0.66f),
            new PointF(outer.Left + size * 0.38f, outer.Top + size * 0.86f),
            new PointF(outer.Left + size * 0.34f, outer.Top + size * 0.62f)
        };

        using (var planeBrush = new SolidBrush(Color.White))
        using (var planePen = new Pen(DarkSlate, Math.Max(1.5f, size * 0.025f)))
        {
            planePen.LineJoin = LineJoin.Round;
            graphics.FillPolygon(planeBrush, planePoints);
            graphics.DrawPolygon(planePen, planePoints);
        }

        using (var dotBrush = new SolidBrush(AccentCyan))
        {
            var dotSize = size * 0.075f;
            graphics.FillEllipse(
                dotBrush,
                outer.Right - size * 0.22f,
                outer.Bottom - size * 0.22f,
                dotSize,
                dotSize);
        }

        return bitmap;
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
