using System.ComponentModel;
using System.Drawing.Drawing2D;
using DevLiteServer.Services;

namespace DevLiteServer.UI;

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
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        Size = new Size(96, 24);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color parentBg = Parent?.BackColor ?? ModernColors.Surface;
        pevent.Graphics.Clear(parentBg);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 1 || Height <= 1) return;

        var g = e.Graphics;
        Color parentBg = Parent?.BackColor ?? ModernColors.Surface;
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Color bg;
        Color border;
        Color dotColor;
        Color textColor;
        string text;

        switch (_status)
        {
            case ServiceStatus.Running:
                bg = Color.FromArgb(16, 45, 30);
                border = Color.FromArgb(34, 197, 94);
                dotColor = ModernColors.Success;
                textColor = Color.FromArgb(220, 252, 231);
                text = "Running";
                break;
            case ServiceStatus.Starting:
                bg = Color.FromArgb(45, 30, 10);
                border = Color.FromArgb(245, 158, 11);
                dotColor = ModernColors.Warning;
                textColor = Color.FromArgb(254, 240, 138);
                text = "Starting...";
                break;
            case ServiceStatus.Stopping:
                bg = Color.FromArgb(45, 30, 10);
                border = Color.FromArgb(245, 158, 11);
                dotColor = ModernColors.Warning;
                textColor = Color.FromArgb(254, 240, 138);
                text = "Stopping...";
                break;
            case ServiceStatus.Error:
                bg = Color.FromArgb(50, 15, 25);
                border = Color.FromArgb(244, 63, 94);
                dotColor = ModernColors.Danger;
                textColor = Color.FromArgb(254, 205, 211);
                text = "Error";
                break;
            default:
                bg = Color.FromArgb(20, 27, 40);
                border = Color.FromArgb(45, 55, 75);
                dotColor = ModernColors.TextMuted;
                textColor = ModernColors.TextSecondary;
                text = "Stopped";
                break;
        }

        float stroke = 1f;
        float halfStroke = stroke / 2f;
        var rect = new RectangleF(halfStroke, halfStroke, Width - stroke, Height - stroke);
        float radius = (Height - stroke) / 2f;

        using var path = CreateRoundedRectangle(rect, radius);
        using var brush = new SolidBrush(bg);
        using var pen = new Pen(border, stroke);

        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        // Draw dot with subtle halo
        float dotSize = 6f;
        float dotX = 10f;
        float dotY = (Height - dotSize) / 2f;

        if (_status == ServiceStatus.Running)
        {
            using var haloBrush = new SolidBrush(Color.FromArgb(50, dotColor));
            g.FillEllipse(haloBrush, dotX - 2, dotY - 2, dotSize + 4, dotSize + 4);
        }

        using var dotBrush = new SolidBrush(dotColor);
        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

        // Draw text
        var textRect = new Rectangle((int)(dotX + dotSize + 6), 0, Width - (int)(dotX + dotSize + 8), Height);
        TextRenderer.DrawText(
            g,
            text,
            Font,
            textRect,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
        );
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float diameter = radius * 2f;
        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;

        var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

        // Top-left
        path.AddArc(arc, 180, 90);

        // Top-right
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom-right
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom-left
        arc.X = rect.X;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}
