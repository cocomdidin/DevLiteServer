using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace DevLiteServer.UI;

public class ModernComboBox : ComboBox
{
    private int _borderRadius = 6;
    private Color _borderLineColor = ModernColors.Border;
    private Color _focusBorderColor = ModernColors.Primary;
    private Color _hoverBorderColor = ModernColors.BorderLight;
    private bool _isHovered;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderRadius
    {
        get => _borderRadius;
        set { _borderRadius = Math.Max(0, value); Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderLineColor
    {
        get => _borderLineColor;
        set { _borderLineColor = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FocusBorderColor
    {
        get => _focusBorderColor;
        set { _focusBorderColor = value; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBorderColor
    {
        get => _hoverBorderColor;
        set { _hoverBorderColor = value; Invalidate(); }
    }

    public ModernComboBox()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 24;
        BackColor = ModernColors.Card;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 8.5f);
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

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnDropDownClosed(EventArgs e)
    {
        base.OnDropDownClosed(e);
        Invalidate();
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Enabled)
        {
            DroppedDown = !DroppedDown;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 1 || Height <= 1) return;
        var g = e.Graphics;

        Color parentBg = Parent?.BackColor ?? ModernColors.Background;
        g.Clear(parentBg);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float stroke = 1f;
        float halfStroke = stroke / 2f;
        var rect = new RectangleF(halfStroke, halfStroke, Width - stroke, Height - stroke);

        using var path = CreateRoundedRectangle(rect, _borderRadius);
        using var bgBrush = new SolidBrush(BackColor);
        g.FillPath(bgBrush, path);

        bool isActive = Focused || DroppedDown;
        Color borderColor = isActive ? _focusBorderColor : (_isHovered ? _hoverBorderColor : _borderLineColor);
        using var borderPen = new Pen(borderColor, stroke);
        g.DrawPath(borderPen, path);

        // Chevron down icon
        float arrowSize = 10f;
        float arrowX = Width - arrowSize - 10f;
        float arrowY = (Height - arrowSize) / 2f;
        var arrowRect = new RectangleF(arrowX, arrowY, arrowSize, arrowSize);
        VectorIcons.Draw(g, IconKind.ChevronDown, arrowRect, isActive ? _focusBorderColor : ModernColors.TextSecondary);

        // Selected Text
        string? text = SelectedItem != null ? GetItemText(SelectedItem) : Text;
        if (!string.IsNullOrEmpty(text))
        {
            int padLeft = Math.Max(8, _borderRadius + 2);
            var textRect = new Rectangle(padLeft, 0, Math.Max(0, (int)arrowX - padLeft - 4), Height);
            TextRenderer.DrawText(g, text, Font, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;

        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color bg = isSelected ? _focusBorderColor : BackColor;
        Color fg = isSelected ? Color.White : ForeColor;

        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        string? itemText = GetItemText(Items[e.Index]);
        if (!string.IsNullOrEmpty(itemText))
        {
            var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, itemText, Font, textRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2f;
        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;

        var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.X;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
