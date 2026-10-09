using System.Drawing.Drawing2D;

namespace DevLiteServer.UI;

public class NavButton : Control
{
    private bool _isActive;
    private bool _isHovered;
    private IconKind _icon = IconKind.None;
    private string? _badgeText;

    public string Title
    {
        get => Text;
        set => Text = value;
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                Invalidate();
            }
        }
    }

    public IconKind Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                Invalidate();
            }
        }
    }

    public string? BadgeText
    {
        get => _badgeText;
        set
        {
            if (_badgeText != value)
            {
                _badgeText = value;
                Invalidate();
            }
        }
    }

    public NavButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        DoubleBuffered = true;
        Height = 42;
        Dock = DockStyle.Top;
        Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
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
        Invalidate();
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

        // Background card fill
        if (_isActive)
        {
            using var activeBg = new SolidBrush(Color.FromArgb(24, 34, 52));
            var fillRect = new RectangleF(8, 2, Width - 16, Height - 4);
            using var path = CreateRoundedRectangle(fillRect, 6f);
            g.FillPath(activeBg, path);

            // Left cyan indicator stripe
            using var barBrush = new SolidBrush(ModernColors.Primary);
            var barRect = new RectangleF(8, 7, 3.5f, Height - 14);
            using var barPath = CreateRoundedRectangle(barRect, 1.75f);
            g.FillPath(barBrush, barPath);
        }
        else if (_isHovered)
        {
            using var hoverBg = new SolidBrush(Color.FromArgb(20, 28, 42));
            var fillRect = new RectangleF(8, 2, Width - 16, Height - 4);
            using var path = CreateRoundedRectangle(fillRect, 6f);
            g.FillPath(hoverBg, path);
        }

        // Vector Icon
        float iconSize = 15f;
        var iconRect = new RectangleF(22, (Height - iconSize) / 2f, iconSize, iconSize);
        Color iconColor = _isActive
            ? ModernColors.Primary
            : (_isHovered ? ModernColors.TextPrimary : ModernColors.TextSecondary);

        VectorIcons.Draw(g, _icon, iconRect, iconColor);

        // Label Text
        Color textColor = _isActive
            ? Color.White
            : (_isHovered ? ModernColors.TextPrimary : ModernColors.TextSecondary);

        using var font = new Font(Font.FontFamily, 9.25f, _isActive ? FontStyle.Bold : FontStyle.Regular);
        var textRect = new Rectangle(46, 0, Width - 54, Height);
        TextRenderer.DrawText(
            g,
            Text,
            font,
            textRect,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
        );

        // Optional right-aligned Badge
        if (!string.IsNullOrEmpty(_badgeText))
        {
            var badgeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            var badgeSize = TextRenderer.MeasureText(g, _badgeText, badgeFont);
            float bw = badgeSize.Width + 10;
            float bh = 18;
            float bx = Width - bw - 14;
            float by = (Height - bh) / 2f;

            var bRect = new RectangleF(bx, by, bw, bh);
            using var bPath = CreateRoundedRectangle(bRect, 4f);
            using var bBrush = new SolidBrush(Color.FromArgb(16, 45, 30));
            using var bPen = new Pen(Color.FromArgb(34, 197, 94, 140), 1f);
            g.FillPath(bBrush, bPath);
            g.DrawPath(bPen, bPath);

            TextRenderer.DrawText(
                g,
                _badgeText,
                badgeFont,
                Rectangle.Round(bRect),
                ModernColors.Success,
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

        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
