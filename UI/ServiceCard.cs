using System.Drawing.Drawing2D;
using LiteServer.Services;

namespace LiteServer.UI;

public class ServiceCard : Panel
{
    private readonly IService _service;
    private readonly StatusPill _pill;
    private readonly ModernButton _btnToggle;
    private readonly Label _lblTitle;
    private readonly Label _lblSub;
    private readonly ComboBox? _cmbVersions;

    public IService Service => _service;

    public ServiceCard(IService service, string icon = "⚡", string[]? versions = null, string? activeVersion = null, Action<string>? onVersionChanged = null)
    {
        _service = service;

        Height = 58;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(16, 8, 16, 8);
        BackColor = ModernColors.Surface;
        DoubleBuffered = true;

        // Service Icon & Title
        _lblTitle = new Label
        {
            Text = $"{icon}  {service.Name}",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 10)
        };

        // Port & Subtitle
        _lblSub = new Label
        {
            Text = $"Port: {service.Port}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(18, 32)
        };

        // Status Pill
        _pill = new StatusPill
        {
            Status = service.Status,
            Location = new Point(310, 16)
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
                Width = 100,
                Location = new Point(190, 18)
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
            Width = 90,
            Height = 32,
            Location = new Point(440, 13),
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            PressedColor = ModernColors.SuccessBg,
            BorderRadius = 6,
            ShowBorder = false
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
            _btnToggle.NormalColor = ModernColors.Danger;
            _btnToggle.HoverColor = ModernColors.DangerHover;
            _btnToggle.PressedColor = ModernColors.DangerBg;
        }
        else if (status == ServiceStatus.Starting || status == ServiceStatus.Stopping)
        {
            _btnToggle.Text = "...";
            _btnToggle.NormalColor = ModernColors.Warning;
            _btnToggle.HoverColor = ModernColors.Warning;
            _btnToggle.PressedColor = ModernColors.WarningBg;
        }
        else
        {
            _btnToggle.Text = "Start";
            _btnToggle.NormalColor = ModernColors.Success;
            _btnToggle.HoverColor = ModernColors.SuccessHover;
            _btnToggle.PressedColor = ModernColors.SuccessBg;
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        // Anchor right-side controls
        if (_btnToggle != null && _pill != null)
        {
            _btnToggle.Location = new Point(Width - _btnToggle.Width - 16, 13);
            _pill.Location = new Point(_btnToggle.Left - _pill.Width - 14, 16);
            if (_cmbVersions != null)
            {
                _cmbVersions.Location = new Point(_pill.Left - _cmbVersions.Width - 14, 17);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = 8;

        using var path = CreateRoundedRectangle(rect, radius);
        using var pen = new Pen(ModernColors.Border, 1);
        g.DrawPath(pen, path);
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
