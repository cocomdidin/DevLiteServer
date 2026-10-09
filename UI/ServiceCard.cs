using System.Diagnostics;
using System.Drawing.Drawing2D;
using DevLiteServer.Services;

namespace DevLiteServer.UI;

public class ServiceCard : Panel
{
    private readonly IService _service;
    private readonly IconKind _iconKind;
    private readonly string _categoryTag;
    private readonly string? _webUrl;

    private readonly StatusPill _pill;
    private readonly ModernButton _btnToggle;
    private readonly ModernButton? _btnBrowse;
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

        // Determine category tag and quick browser URL
        switch (service.Name.ToLowerInvariant())
        {
            case "nginx":
                _categoryTag = "Web Server";
                _webUrl = $"http://localhost:{service.Port}";
                break;
            case "php":
            case "php-cgi":
                _categoryTag = "FastCGI Runtime";
                _webUrl = null;
                break;
            case "mysql":
                _categoryTag = "Database Engine";
                _webUrl = null;
                break;
            case "mailpit":
                _categoryTag = "Mail Inbox & SMTP";
                _webUrl = $"http://localhost:{service.Port}";
                break;
            case "postgresql":
                _categoryTag = "Relational DB";
                _webUrl = null;
                break;
            case "redis":
                _categoryTag = "In-Memory Cache";
                _webUrl = null;
                break;
            default:
                _categoryTag = "Background Service";
                _webUrl = null;
                break;
        }

        Height = 62;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(16, 8, 16, 8);
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
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(62, 11)
        };

        // Port & Category Subtitle
        _lblSub = new Label
        {
            Text = $"{_categoryTag}  •  Port: {service.Port}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(63, 33)
        };

        // Status Pill
        _pill = new StatusPill
        {
            Status = service.Status,
            Location = new Point(360, 19)
        };

        // Optional Quick Web Browser Button (for Nginx & Mailpit)
        if (!string.IsNullOrEmpty(_webUrl))
        {
            _btnBrowse = new ModernButton
            {
                Text = service.Name.Equals("nginx", StringComparison.OrdinalIgnoreCase) ? "Open Web" : "Open Inbox",
                IconKind = IconKind.Globe,
                IconSize = 10,
                Width = 84,
                Height = 30,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.Primary,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Visible = service.Status == ServiceStatus.Running
            };
            _btnBrowse.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = _webUrl, UseShellExecute = true });
                }
                catch { }
            };
            Controls.Add(_btnBrowse);
        }

        // Version dropdown if available (e.g. PHP)
        if (versions != null && versions.Length > 0)
        {
            _cmbVersions = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ModernColors.Card,
                ForeColor = ModernColors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Width = 100,
                Location = new Point(240, 18)
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

        // Action Start/Stop Button
        _btnToggle = new ModernButton
        {
            Text = "Start",
            IconKind = IconKind.Play,
            IconSize = 10,
            Width = 84,
            Height = 32,
            Location = new Point(500, 15),
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

    public void RefreshInfo()
    {
        _lblSub.Text = $"{_categoryTag}  •  Port: {_service.Port}";
        UpdateVisuals(_service.Status);
    }

    private void UpdateVisuals(ServiceStatus status)
    {
        _lblSub.Text = $"{_categoryTag}  •  Port: {_service.Port}";
        _pill.Status = status;

        if (_btnBrowse != null)
        {
            _btnBrowse.Visible = status == ServiceStatus.Running;
        }

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

        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        BackColor = ModernColors.CardHover;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        BackColor = ModernColors.Surface;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Color parentBg = Parent?.BackColor ?? ModernColors.Background;
        pevent.Graphics.Clear(parentBg);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);

        // Dynamic Right-to-Left alignment
        if (_btnToggle != null && _pill != null)
        {
            int rightOffset = Width - 14;

            // 1. Toggle Button (Rightmost)
            _btnToggle.Location = new Point(rightOffset - _btnToggle.Width, (Height - _btnToggle.Height) / 2);
            rightOffset = _btnToggle.Left - 10;

            // 2. Status Pill
            _pill.Location = new Point(rightOffset - _pill.Width, (Height - _pill.Height) / 2);
            rightOffset = _pill.Left - 10;

            // 3. Optional Browse Button
            if (_btnBrowse != null)
            {
                _btnBrowse.Location = new Point(rightOffset - _btnBrowse.Width, (Height - _btnBrowse.Height) / 2);
                if (_btnBrowse.Visible)
                {
                    rightOffset = _btnBrowse.Left - 10;
                }
            }

            // 4. Optional Version ComboBox
            if (_cmbVersions != null)
            {
                _cmbVersions.Location = new Point(rightOffset - _cmbVersions.Width, (Height - _cmbVersions.Height) / 2);
            }
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

        using var path = CreateRoundedRectangle(rect, 8f);
        using var bgBrush = new SolidBrush(BackColor);
        g.FillPath(bgBrush, path);

        using var pen = new Pen(_isHovered ? ModernColors.BorderLight : ModernColors.BorderSubtle, stroke);
        g.DrawPath(pen, path);

        // Active running indicator bar on left edge (3px)
        if (_service.Status == ServiceStatus.Running)
        {
            using var activeBrush = new SolidBrush(ModernColors.Success);
            using var activePath = new GraphicsPath();
            activePath.AddArc(rect.X, rect.Y, 8f, 8f, 180, 90);
            activePath.AddLine(rect.X + 3f, rect.Y, rect.X + 3f, rect.Bottom);
            activePath.AddArc(rect.X, rect.Bottom - 8f, 8f, 8f, 90, 90);
            activePath.CloseFigure();
            g.FillPath(activeBrush, activePath);
        }

        // Draw left vector icon badge (34x34)
        var badgeRect = new RectangleF(14f + halfStroke, (Height - 34f) / 2f + halfStroke, 34f - stroke, 34f - stroke);
        using var badgePath = CreateRoundedRectangle(badgeRect, 6f);
        using var badgeBg = new SolidBrush(_service.Status == ServiceStatus.Running ? Color.FromArgb(16, 36, 32) : ModernColors.Card);
        g.FillPath(badgeBg, badgePath);

        using var badgeBorder = new Pen(_service.Status == ServiceStatus.Running ? Color.FromArgb(34, 197, 94, 120) : ModernColors.BorderSubtle, stroke);
        g.DrawPath(badgeBorder, badgePath);

        // Draw Vector Icon inside badge
        var iconRect = new RectangleF(14f + 8f, (Height - 34f) / 2f + 8f, 18f, 18f);
        Color iconColor = _service.Status == ServiceStatus.Running ? ModernColors.Success : ModernColors.Primary;
        VectorIcons.Draw(g, _iconKind, iconRect, iconColor);
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
