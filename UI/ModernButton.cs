using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DevLiteServer.UI;

public class ModernButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderRadius { get; set; } = 6;

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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconKind IconKind { get; set; } = IconKind.None;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int IconSize { get; set; } = 14;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color? CustomIconColor { get; set; }

    private bool _isHovered;
    private bool _isPressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
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

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Clear background with parent's BackColor to ensure corner anti-aliasing
        // blends seamlessly with no dirty rectangular edges or artifacts.
        Color parentBg = Parent?.BackColor ?? ModernColors.Background;
        using var bgBrush = new SolidBrush(parentBg);
        pevent.Graphics.FillRectangle(bgBrush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        if (Width <= 1 || Height <= 1) return;

        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        Color currentBg = _isPressed ? PressedColor : (_isHovered ? HoverColor : NormalColor);
        if (!Enabled)
        {
            currentBg = Color.FromArgb(24, 33, 50);
        }

        // Subpixel geometric inset (0.5f) guarantees exact 1.0px border stroke with 0 clipping on all 4 sides
        float stroke = 1f;
        float halfStroke = stroke / 2f;
        var rect = new RectangleF(halfStroke, halfStroke, Width - stroke, Height - stroke);

        using var path = CreateRoundedRectangle(rect, BorderRadius);
        using var brush = new SolidBrush(currentBg);
        g.FillPath(brush, path);

        if (ShowBorder && Enabled)
        {
            using var pen = new Pen(_isHovered ? ModernColors.BorderLight : BorderLineColor, stroke);
            g.DrawPath(pen, path);
        }
        else if (Enabled)
        {
            // Subtle crisp top/inner edge rim for colored solid buttons
            Color rimColor = _isHovered
                ? Color.FromArgb(60, 255, 255, 255)
                : Color.FromArgb(25, 255, 255, 255);
            using var rimPen = new Pen(rimColor, stroke);
            g.DrawPath(rimPen, path);
        }
        else
        {
            using var disabledPen = new Pen(ModernColors.BorderSubtle, stroke);
            g.DrawPath(disabledPen, path);
        }

        // Text & Vector Icon
        Color textColor = Enabled ? (_isHovered ? Color.White : ForeColor) : ModernColors.TextMuted;
        Color iconColor = Enabled && _isHovered ? Color.White : (CustomIconColor ?? textColor);

        if (IconKind != IconKind.None)
        {
            const TextFormatFlags textFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
            var textSize = TextRenderer.MeasureText(g, Text, Font, new Size(Width, Height), textFlags);
            int spacing = string.IsNullOrEmpty(Text) ? 0 : 7;
            float totalContentWidth = IconSize + (string.IsNullOrEmpty(Text) ? 0 : spacing + textSize.Width);
            float startX = (Width - totalContentWidth) / 2f;
            float iconY = (Height - IconSize) / 2f;

            var iconRect = new RectangleF(startX, iconY, IconSize, IconSize);
            VectorIcons.Draw(g, IconKind, iconRect, iconColor);

            if (!string.IsNullOrEmpty(Text))
            {
                var textRect = new Rectangle((int)(startX + IconSize + spacing), 0, textSize.Width + 2, Height);
                TextRenderer.DrawText(
                    g,
                    Text,
                    Font,
                    textRect,
                    textColor,
                    textFlags
                );
            }
        }
        else
        {
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                ClientRectangle,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            );
        }
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
