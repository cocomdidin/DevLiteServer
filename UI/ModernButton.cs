using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DevLiteServer.UI;

public class ModernButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderRadius { get; set; } = 8;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NormalColor { get; set; } = ModernColors.Surface;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = ModernColors.SurfaceHover;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PressedColor { get; set; } = ModernColors.Card;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderLineColor { get; set; } = ModernColors.Border;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowBorder { get; set; } = true;

    private bool _isHovered;
    private bool _isPressed;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = NormalColor;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        _isPressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color currentBg = _isPressed ? PressedColor : (_isHovered ? HoverColor : NormalColor);
        if (!Enabled)
        {
            currentBg = Color.FromArgb(40, 50, 70);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = CreateRoundedRectangle(rect, BorderRadius);
        using var brush = new SolidBrush(currentBg);
        g.FillPath(brush, path);

        if (ShowBorder && Enabled)
        {
            using var pen = new Pen(_isHovered ? ModernColors.BorderLight : BorderLineColor, 1);
            g.DrawPath(pen, path);
        }

        // Text
        Color textColor = Enabled ? ForeColor : ModernColors.TextMuted;
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            rect,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
        );
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
