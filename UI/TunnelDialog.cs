using System.Diagnostics;
using DevLiteServer.Core;
using DevLiteServer.Core.Downloader;
using DevLiteServer.Services;

namespace DevLiteServer.UI;

public class TunnelDialog : Form
{
    private readonly string _appRoot;
    private readonly JobObject _job;
    private readonly SiteItem _site;
    private readonly AppConfig _config;

    private readonly StatusPill _pillStatus;
    private readonly ModernTextBox _txtUrl;
    private readonly ModernButton _btnCopy;
    private readonly ModernButton _btnOpen;
    private readonly ModernButton _btnToggle;
    private readonly TextBox _txtLogs;
    private readonly Label _lblInfo;

    private TunnelSession? _session;
    private Action<string>? _logHandler;
    private Action<string>? _urlHandler;

    public TunnelDialog(string appRoot, JobObject job, SiteItem site, AppConfig config)
    {
        _appRoot = appRoot;
        _job = job;
        _site = site;
        _config = config;

        Text = $"Public Share Tunnel - {site.Domain}";
        ClientSize = new Size(580, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = false;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = ModernColors.Surface,
            Padding = new Padding(22, 14, 22, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var p = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = $"Cloudflare Tunnel: {site.Domain}",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 12)
        };

        var lblSub = new Label
        {
            Text = "One-click instant public HTTPS URL via Cloudflare (no port forwarding or account required).",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(21, 36)
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSub);

        // Body
        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16)
        };

        int top = 16;
        int fieldW = ClientSize.Width - 48;

        // Status pill row
        var lblStatusTitle = new Label
        {
            Text = "Tunnel Status:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top + 4),
            AutoSize = true
        };

        _pillStatus = new StatusPill
        {
            Text = "Stopped",
            Status = ServiceStatus.Stopped,
            Location = new Point(130, top),
            Size = new Size(90, 24)
        };

        _btnToggle = new ModernButton
        {
            Text = "Start Tunnel",
            IconKind = IconKind.Lightning,
            IconSize = 10,
            Location = new Point(fieldW - 90, top),
            Width = 114,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        _btnToggle.Click += async (s, e) => await ToggleTunnelAsync();

        top += 38;

        // Public URL Box
        var lblUrl = new Label
        {
            Text = "Public HTTPS URL:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += 20;

        _txtUrl = new ModernTextBox
        {
            Location = new Point(24, top),
            Width = fieldW - 175,
            Height = 28,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderRadius = 6,
            ReadOnly = true,
            Text = "Not connected"
        };

        _btnCopy = new ModernButton
        {
            Text = "Copy",
            IconKind = IconKind.Check,
            IconSize = 10,
            Location = new Point(_txtUrl.Right + 8, top - 1),
            Width = 78,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Enabled = false
        };
        _btnCopy.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_txtUrl.Text) && _txtUrl.Text.StartsWith("http"))
            {
                Clipboard.SetText(_txtUrl.Text);
                _btnCopy.Text = "Copied!";
                Task.Delay(1500).ContinueWith(_ =>
                {
                    if (!IsDisposed) Invoke(() => _btnCopy.Text = "Copy");
                });
            }
        };

        _btnOpen = new ModernButton
        {
            Text = "Open",
            IconKind = IconKind.Globe,
            IconSize = 10,
            Location = new Point(_btnCopy.Right + 8, top - 1),
            Width = 78,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Enabled = false
        };
        _btnOpen.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_txtUrl.Text) && _txtUrl.Text.StartsWith("http"))
            {
                try { Process.Start(new ProcessStartInfo(_txtUrl.Text) { UseShellExecute = true }); } catch { }
            }
        };

        top += 40;

        // Terminal Output / Logs
        var lblLogs = new Label
        {
            Text = "Tunnel Activity Logs:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += 20;

        _txtLogs = new TextBox
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = 130,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(16, 20, 28),
            ForeColor = Color.FromArgb(180, 200, 220),
            Font = new Font("Cascadia Code", 8.25f),
            BorderStyle = BorderStyle.FixedSingle
        };

        top += 140;

        _lblInfo = new Label
        {
            Location = new Point(24, top),
            Width = fieldW,
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8f),
            Text = "Tunnels are securely routed to your local project and automatically close when stopped or app exits."
        };

        body.Controls.AddRange([
            lblStatusTitle, _pillStatus, _btnToggle,
            lblUrl, _txtUrl, _btnCopy, _btnOpen,
            lblLogs, _txtLogs,
            _lblInfo
        ]);

        Controls.Add(body);
        Controls.Add(pnlHeader);

        // Check if session is already running
        _session = CloudflareTunnelManager.GetSession(_site.Domain);
        if (_session != null && _session.IsActive)
        {
            AttachSession(_session);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        DetachSessionHandlers();
        base.OnFormClosed(e);
    }

    private void DetachSessionHandlers()
    {
        if (_session != null)
        {
            if (_logHandler != null) _session.LogReceived -= _logHandler;
            if (_urlHandler != null) _session.UrlDiscovered -= _urlHandler;
            _logHandler = null;
            _urlHandler = null;
        }
    }

    private void AttachSession(TunnelSession session)
    {
        DetachSessionHandlers();

        _session = session;
        _pillStatus.Text = session.Status;
        _pillStatus.Status = session.Status == "Online" ? ServiceStatus.Running : ServiceStatus.Starting;

        _btnToggle.Text = "Stop Tunnel";
        _btnToggle.NormalColor = ModernColors.Danger;
        _btnToggle.HoverColor = ModernColors.DangerHover;

        if (!string.IsNullOrEmpty(session.PublicUrl))
        {
            _txtUrl.Text = session.PublicUrl;
            _btnCopy.Enabled = true;
            _btnOpen.Enabled = true;
        }

        _logHandler = line =>
        {
            if (IsDisposed) return;
            try
            {
                Invoke(() =>
                {
                    _txtLogs.AppendText(line + Environment.NewLine);
                });
            }
            catch { }
        };

        _urlHandler = url =>
        {
            if (IsDisposed) return;
            try
            {
                Invoke(() =>
                {
                    _txtUrl.Text = url;
                    _pillStatus.Text = "Online";
                    _pillStatus.Status = ServiceStatus.Running;
                    _btnCopy.Enabled = true;
                    _btnOpen.Enabled = true;
                });
            }
            catch { }
        };

        session.LogReceived += _logHandler;
        session.UrlDiscovered += _urlHandler;
    }

    private async Task ToggleTunnelAsync()
    {
        if (_session != null && _session.IsActive)
        {
            // Stop
            DetachSessionHandlers();
            CloudflareTunnelManager.StopTunnel(_site.Domain);
            _session = null;
            _pillStatus.Text = "Stopped";
            _pillStatus.Status = ServiceStatus.Stopped;
            _btnToggle.Text = "Start Tunnel";
            _btnToggle.NormalColor = ModernColors.Success;
            _btnToggle.HoverColor = ModernColors.SuccessHover;
            _txtUrl.Text = "Not connected";
            _btnCopy.Enabled = false;
            _btnOpen.Enabled = false;
            _txtLogs.AppendText($"[Info] Tunnel stopped for {_site.Domain}{Environment.NewLine}");
            return;
        }

        // Start
        if (!CloudflareTunnelManager.IsInstalled(_appRoot))
        {
            var res = MessageBox.Show(this,
                "Cloudflare Tunnel (cloudflared.exe) is not installed.\n\nWould you like Dev Lite Server to download it automatically now?",
                "Install Cloudflare Tunnel",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                var pkg = PackageCatalog.GetServicePackage("cloudflared");
                if (pkg != null)
                {
                    _txtLogs.AppendText("[Info] Downloading cloudflared.exe from official Cloudflare releases...\r\n");
                    _btnToggle.Enabled = false;
                    var progress = new Progress<PackageDownloadProgress>(p =>
                    {
                        if (!IsDisposed) Invoke(() => _pillStatus.Text = $"{p.Percent:F0}%");
                    });
                    bool ok = await PackageDownloader.DownloadAndInstallAsync(pkg, _appRoot, progress);
                    _btnToggle.Enabled = true;

                    if (!ok)
                    {
                        MessageBox.Show(this, "Failed to download cloudflared.exe. Please check your internet connection.", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _pillStatus.Text = "Stopped";
                        _pillStatus.Status = ServiceStatus.Stopped;
                        return;
                    }
                    _txtLogs.AppendText("[Info] cloudflared.exe installed successfully.\r\n");
                }
            }
            else
            {
                return;
            }
        }

        try
        {
            _btnToggle.Enabled = false;
            _pillStatus.Text = "Connecting...";
            _pillStatus.Status = ServiceStatus.Starting;
            _txtLogs.AppendText($"[Info] Starting tunnel for {_site.Domain}...\r\n");

            var session = await CloudflareTunnelManager.StartTunnelAsync(_appRoot, _job, _site, _config);
            AttachSession(session);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Tunnel Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _pillStatus.Text = "Error";
            _pillStatus.Status = ServiceStatus.Error;
        }
        finally
        {
            _btnToggle.Enabled = true;
        }
    }
}
