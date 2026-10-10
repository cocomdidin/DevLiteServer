using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace DevLiteServer.UI;

[DefaultEvent(nameof(TextChanged))]
public class ModernTextBox : Control
{
    private readonly TextBox _innerBox;
    private bool _isHovered;
    private bool _isFocused;
    private int _borderRadius = 6;
    private Color _borderLineColor = ModernColors.Border;
    private Color _focusBorderColor = ModernColors.Primary;
    private Color _hoverBorderColor = ModernColors.BorderLight;

    public TextBox InnerTextBox => _innerBox;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderRadius
    {
        get => _borderRadius;
        set { _borderRadius = Math.Max(0, value); UpdateInnerLayout(); Invalidate(); }
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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [AllowNull]
    public override string Text
    {
        get => _innerBox.Text;
        set => _innerBox.Text = value ?? "";
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => _innerBox.PlaceholderText;
        set => _innerBox.PlaceholderText = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ReadOnly
    {
        get => _innerBox.ReadOnly;
        set => _innerBox.ReadOnly = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Multiline
    {
        get => _innerBox.Multiline;
        set { _innerBox.Multiline = value; UpdateInnerLayout(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HorizontalAlignment TextAlign
    {
        get => _innerBox.TextAlign;
        set => _innerBox.TextAlign = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public char PasswordChar
    {
        get => _innerBox.PasswordChar;
        set => _innerBox.PasswordChar = value;
    }

    public ModernTextBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = ModernColors.Card;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 8.5f);
        Size = new Size(120, 26);

        _innerBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = BackColor,
            ForeColor = ForeColor,
            Font = Font
        };

        _innerBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
        _innerBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
        _innerBox.TextChanged += (s, e) => OnTextChanged(e);
        _innerBox.KeyDown += (s, e) => OnKeyDown(e);
        _innerBox.KeyUp += (s, e) => OnKeyUp(e);
        _innerBox.KeyPress += (s, e) => OnKeyPress(e);

        Controls.Add(_innerBox);
        UpdateInnerLayout();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        _innerBox.Focus();
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

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        if (_innerBox != null) _innerBox.BackColor = BackColor;
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        if (_innerBox != null) _innerBox.ForeColor = ForeColor;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_innerBox != null)
        {
            _innerBox.Font = Font;
            UpdateInnerLayout();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateInnerLayout();
    }

    private void UpdateInnerLayout()
    {
        if (_innerBox == null) return;
        int padX = Math.Max(8, _borderRadius + 2);
        if (_innerBox.Multiline)
        {
            int padY = Math.Max(4, _borderRadius / 2);
            _innerBox.SetBounds(padX, padY, Math.Max(10, Width - (padX * 2)), Math.Max(10, Height - (padY * 2)));
        }
        else
        {
            int tbH = _innerBox.PreferredHeight;
            int tbY = Math.Max(2, (Height - tbH) / 2);
            _innerBox.SetBounds(padX, tbY, Math.Max(10, Width - (padX * 2)), tbH);
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

        Color borderColor = _isFocused ? _focusBorderColor : (_isHovered ? _hoverBorderColor : _borderLineColor);
        using var borderPen = new Pen(borderColor, stroke);
        g.DrawPath(borderPen, path);
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
