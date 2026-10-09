using System.Drawing.Drawing2D;
using DevLiteServer.Services;

namespace DevLiteServer.UI;

public class ServiceCard : Panel
{
    private readonly IService _service;
    private readonly IconKind _iconKind;
    private readonly StatusPill _pill;
    private readonly ModernButton _btnToggle;
    private readonly Label _lblTitle;
    private readonly Label _lblSub;
    private readonly ComboBox? _cmbVersions;
    private bool _isHovered;

    public IService Service => _service;

    public ServiceCard(
        IService service,
        IconKind iconKind = IconKind.Server,
        string[]? versions = null,
        string? activeVersion = null,
        Action<string>? onVersionChanged = null)
    {
        _service = service;
        _iconKind = iconKind;

        Height = 58;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(14, 8, 14, 8);
        BackColor = ModernColors.Surface;
        DoubleBuffered = true;

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        // Service Title
        _lblTitle = new Label
        {
            Text = service.Name,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(56, 11)
        };

        // Port & Subtitle
        _lblSub = new Label
        {
            Text = $"Port: {service.Port}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(57, 31)
        };

        // Status Pill
        _pill = new StatusPill
        {
            Status = service.Status,
            Location = new Point(320, 17)
        };

        // Version dropdown if available
        if (versions != null && versions.Length > 0)
        {
            _cmbVersions = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ModernColors.Card,
                ForeColor = ModernColors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Width = 95,
                Location = new Point(210, 17)
            };
            _cmbVersions.Items.AddRange(versions);
            if (!string.IsNullOrEmpty(activeVersion) && _cmbVersions.Items.Contains(activeVersion))
            {
                _cmbVersions.SelectedItem = activeVersion;
            }
            else if (_cmbVersions.Items.Count > 0)
            {
                _cmbVersions.SelectedIndex = 0;
            }

            _cmbVersions.SelectedIndexChanged += (s, e) =>
            {
                if (_cmbVersions.SelectedItem is string selected)
                {
                    onVersionChanged?.Invoke(selected);
                }
            };
            Controls.Add(_cmbVersions);
        }

        // Action Button
        _btnToggle = new ModernButton
        {
            Text = "Start",
            IconKind = IconKind.Play,
            IconSize = 10,
            Width = 84,
            Height = 30,
            Location = new Point(440, 14),
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            PressedColor = ModernColors.SuccessBg,
            BorderRadius = 6,
            ShowBorder = false,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        _btnToggle.Click += async (s, e) =>
        {
            _btnToggle.Enabled = false;
            if (_service.Status == ServiceStatus.Running)
            {
                await _service.StopAsync();
            }
            else
            {
                await _service.StartAsync();
            }
            _btnToggle.Enabled = true;
        };

        Controls.Add(_lblTitle);
        Controls.Add(_lblSub);
        Controls.Add(_pill);
        Controls.Add(_btnToggle);

        service.StatusChanged += OnStatusChanged;
        UpdateVisuals(service.Status);
    }

    private void OnStatusChanged(IService svc, ServiceStatus status)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateVisuals(status));
        }
        else
        {
            UpdateVisuals(status);
        }
    }

    private void UpdateVisuals(ServiceStatus status)
    {
        _pill.Status = status;

        if (status == ServiceStatus.Running)
        {
            _btnToggle.Text = "Stop";
            _btnToggle.IconKind = IconKind.Stop;
            _btnToggle.NormalColor = ModernColors.Danger;
            _btnToggle.HoverColor = ModernColors.DangerHover;
            _btnToggle.PressedColor = ModernColors.DangerBg;
        }
        else if (status == ServiceStatus.Starting || status == ServiceStatus.Stopping)
        {
            _btnToggle.Text = "...";
            _btnToggle.IconKind = IconKind.None;
            _btnToggle.NormalColor = ModernColors.Warning;
            _btnToggle.HoverColor = ModernColors.WarningHover;
            _btnToggle.PressedColor = ModernColors.WarningBg;
        }
        else
        {
            _btnToggle.Text = "Start";
            _btnToggle.IconKind = IconKind.Play;
            _btnToggle.NormalColor = ModernColors.Success;
            _btnToggle.HoverColor = ModernColors.SuccessHover;
            _btnToggle.PressedColor = ModernColors.SuccessBg;
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        BackColor = ModernColors.CardHover;
        _btnToggle?.Invalidate();
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        BackColor = ModernColors.Surface;
        _btnToggle?.Invalidate();
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color parentBg = Parent?.BackColor ?? ModernColors.Background;
        using var bgBrush = new SolidBrush(parentBg);
        pevent.Graphics.FillRectangle(bgBrush, ClientRectangle);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        // Anchor right-side controls dynamically
        if (_btnToggle != null && _pill != null)
        {
            _btnToggle.Location = new Point(Width - _btnToggle.Width - 14, (Height - _btnToggle.Height) / 2);
            _pill.Location = new Point(_btnToggle.Left - _pill.Width - 12, (Height - _pill.Height) / 2);
            if (_cmbVersions != null)
            {
                _cmbVersions.Location = new Point(_pill.Left - _cmbVersions.Width - 12, (Height - _cmbVersions.Height) / 2);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 1 || Height <= 1) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float stroke = 1f;
        float halfStroke = stroke / 2f;
        var rect = new RectangleF(halfStroke, halfStroke, Width - stroke, Height - stroke);

        using var path = CreateRoundedRectangle(rect, 8f);
        using var bgBrush = new SolidBrush(BackColor);
        g.FillPath(bgBrush, path);

        using var pen = new Pen(_isHovered ? ModernColors.BorderLight : ModernColors.Border, stroke);
        g.DrawPath(pen, path);

        // Draw left vector icon badge (32x32)
        var badgeRect = new RectangleF(14f + halfStroke, (Height - 32f) / 2f + halfStroke, 32f - stroke, 32f - stroke);
        using var badgePath = CreateRoundedRectangle(badgeRect, 6f);
        using var badgeBg = new SolidBrush(ModernColors.Card);
        g.FillPath(badgeBg, badgePath);
        using var badgeBorder = new Pen(ModernColors.BorderSubtle, stroke);
        g.DrawPath(badgeBorder, badgePath);

        // Draw Vector Icon inside badge
        var iconRect = new RectangleF(14f + 7f, (Height - 32f) / 2f + 7f, 18f, 18f);
        VectorIcons.Draw(g, _iconKind, iconRect, ModernColors.Primary);
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
