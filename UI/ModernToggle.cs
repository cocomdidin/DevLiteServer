using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DevLiteServer.UI;

[DefaultEvent(nameof(CheckedChanged))]
public class ModernToggle : Control
{
    private bool _checked;
    private bool _isHovered;

    public event EventHandler? CheckedChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OnColor { get; set; } = ModernColors.Success;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OffColor { get; set; } = Color.FromArgb(30, 41, 59); // Slate 800

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ThumbColor { get; set; } = Color.White;

    public ModernToggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        Size = new Size(44, 24);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
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
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color parentBg = Parent?.BackColor ?? ModernColors.Surface;
        pevent.Graphics.Clear(parentBg);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color parentBg = Parent?.BackColor ?? ModernColors.Surface;
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float stroke = 1f;
        float halfStroke = stroke / 2f;
        var trackRect = new RectangleF(halfStroke, halfStroke, Width - stroke, Height - stroke);

        Color currentTrackColor = _checked
            ? (_isHovered ? ModernColors.SuccessHover : OnColor)
            : (_isHovered ? Color.FromArgb(45, 55, 75) : OffColor);

        // Draw pill track
        using var trackPath = CreatePillPath(trackRect);
        using var trackBrush = new SolidBrush(currentTrackColor);
        g.FillPath(trackBrush, trackPath);

        using var trackPen = new Pen(_checked ? ModernColors.Success : ModernColors.Border, stroke);
        g.DrawPath(trackPen, trackPath);

        // Draw Thumb Circle
        float thumbPadding = 3f;
        float thumbSize = Height - (thumbPadding * 2f);
        float thumbX = _checked ? (Width - thumbSize - thumbPadding) : thumbPadding;
        float thumbY = thumbPadding;

        var thumbRect = new RectangleF(thumbX, thumbY, thumbSize, thumbSize);
        using var thumbBrush = new SolidBrush(ThumbColor);
        g.FillEllipse(thumbBrush, thumbRect);

        // Subtle inner ring on thumb
        using var thumbPen = new Pen(Color.FromArgb(40, 0, 0, 0), 1f);
        g.DrawEllipse(thumbPen, thumbRect);
    }

    private static GraphicsPath CreatePillPath(RectangleF rect)
    {
        var path = new GraphicsPath();
        float radius = rect.Height / 2f;
        float diameter = radius * 2f;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 90, 180);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 180);
        path.CloseFigure();
        return path;
    }
}
