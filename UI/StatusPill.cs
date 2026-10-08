using System.ComponentModel;
using System.Drawing.Drawing2D;
using LiteServer.Services;

namespace LiteServer.UI;

public class StatusPill : Control
{
    private ServiceStatus _status = ServiceStatus.Stopped;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ServiceStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                Invalidate();
            }
        }
    }

    public StatusPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        Size = new Size(110, 26);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color bg;
        Color border;
        Color dotColor;
        Color textColor;
        string text;

        switch (_status)
        {
            case ServiceStatus.Running:
                bg = ModernColors.SuccessBg;
                border = Color.FromArgb(5, 150, 105);
                dotColor = ModernColors.Success;
                textColor = Color.FromArgb(209, 250, 229);
                text = "RUNNING";
                break;
            case ServiceStatus.Starting:
                bg = ModernColors.WarningBg;
                border = Color.FromArgb(180, 83, 9);
                dotColor = ModernColors.Warning;
                textColor = Color.FromArgb(254, 243, 199);
                text = "STARTING";
                break;
            case ServiceStatus.Stopping:
                bg = ModernColors.WarningBg;
                border = Color.FromArgb(180, 83, 9);
                dotColor = ModernColors.Warning;
                textColor = Color.FromArgb(254, 243, 199);
                text = "STOPPING";
                break;
            case ServiceStatus.Error:
                bg = ModernColors.DangerBg;
                border = Color.FromArgb(185, 28, 28);
                dotColor = ModernColors.Danger;
                textColor = Color.FromArgb(254, 202, 202);
                text = "ERROR";
                break;
            default:
                bg = Color.FromArgb(30, 41, 59);
                border = ModernColors.Border;
                dotColor = ModernColors.TextMuted;
                textColor = ModernColors.TextSecondary;
                text = "STOPPED";
                break;
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Height / 2;

        using var path = CreateRoundedRectangle(rect, radius);
        using var brush = new SolidBrush(bg);
        using var pen = new Pen(border, 1);

        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        // Draw dot
        int dotSize = 6;
        int dotX = 10;
        int dotY = (Height - dotSize) / 2;
        using var dotBrush = new SolidBrush(dotColor);
        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

        // Draw text
        var textRect = new Rectangle(dotX + dotSize + 6, 0, Width - (dotX + dotSize + 8), Height);
        TextRenderer.DrawText(
            g,
            text,
            Font,
            textRect,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
        );
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
