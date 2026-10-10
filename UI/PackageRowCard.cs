using System.Drawing.Drawing2D;
using DevLiteServer.Core;
using DevLiteServer.Core.Downloader;
using DevLiteServer.Services;

namespace DevLiteServer.UI;

public class PackageRowCard : Panel
{
    private readonly PackageItem _pkg;
    private readonly string _appRoot;
    private readonly AppConfig _config;
    private readonly Action _onChanged;

    private readonly StatusPill _pill;
    private readonly Label _lblTitle;
    private readonly Label _lblTag;
    private readonly Label _lblStatus;
    private readonly ModernButton _btnAction;
    private readonly ModernButton _btnSwitch;
    private readonly ModernButton _btnDelete;

    private CancellationTokenSource? _cts;
    private bool _isDownloading = false;

    public PackageRowCard(PackageItem pkg, string appRoot, AppConfig config, Action onChanged)
    {
        _pkg = pkg;
        _appRoot = appRoot;
        _config = config;
        _onChanged = onChanged;

        Dock = DockStyle.Top;
        Height = 74;
        BackColor = Color.Transparent;
        Padding = new Padding(14, 6, 14, 10);

        Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = Height - 8f;
            var rect = new RectangleF(halfStroke, halfStroke, Width - stroke, cardH - stroke);
            using var path = CreateRoundedRect(rect, 6f);
            using var bg = new SolidBrush(ModernColors.Surface);
            g.FillPath(bg, path);
            using var pen = new Pen(_pkg.IsActive(_config) ? Color.FromArgb(56, 189, 248, 120) : ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);

            // Active running pill badge indicator on left
            if (_pkg.IsActive(_config))
            {
                using var activeBrush = new SolidBrush(ModernColors.Primary);
                using var activePath = new GraphicsPath();
                activePath.AddArc(rect.X, rect.Y, 6f, 6f, 180, 90);
                activePath.AddLine(rect.X + 3.5f, rect.Y, rect.X + 3.5f, rect.Bottom);
                activePath.AddArc(rect.X, rect.Bottom - 6f, 6f, 6f, 90, 90);
                activePath.CloseFigure();
                g.FillPath(activeBrush, activePath);
            }
        };

        _pill = new StatusPill
        {
            Location = new Point(14, 18)
        };

        _lblTitle = new Label
        {
            Text = _pkg.Name,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(116, 12),
            AutoSize = true
        };

        _lblTag = new Label
        {
            Text = $"{_pkg.Tag}  •  ~{_pkg.ApproximateSizeBytes / (1024 * 1024)} MB",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(117, 34),
            AutoSize = true
        };

        _lblStatus = new Label
        {
            Text = "",
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(117, 34),
            AutoSize = true,
            Visible = false
        };

        _btnAction = new ModernButton
        {
            Width = 95,
            Height = 30,
            BorderRadius = 6,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        _btnSwitch = new ModernButton
        {
            Text = "Active",
            IconKind = IconKind.Check,
            IconSize = 10,
            Width = 78,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        _btnDelete = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Trash,
            IconSize = 11,
            Width = 32,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = Color.FromArgb(45, 20, 28),
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        _btnAction.Click += async (s, e) =>
        {
            if (_isDownloading)
            {
                _cts?.Cancel();
                return;
            }

            if (!_pkg.IsInstalled(_appRoot))
            {
                await StartDownloadAsync();
            }
        };

        _btnSwitch.Click += (s, e) =>
        {
            if (!_pkg.IsInstalled(_appRoot)) return;

            if (_pkg.Category.Equals("PHP", StringComparison.OrdinalIgnoreCase))
            {
                _config.ActivePhp = _pkg.FolderName;
                PhpService.UpdateCurrentJunction(_appRoot, _pkg.FolderName);
            }
            else
            {
                _config.ActiveNode = _pkg.FolderName;
                NodeManager.UpdateCurrentJunction(_appRoot, _pkg.FolderName);
            }

            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            _onChanged();
        };

        _btnDelete.Click += async (s, e) =>
        {
            if (_pkg.IsActive(_config))
            {
                MessageBox.Show(this, "Cannot uninstall currently active runtime. Switch to another version first.", "Runtime Busy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var res = MessageBox.Show(this, $"Uninstall {_pkg.Name} and remove files?", "Uninstall Runtime", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                _btnDelete.Enabled = false;
                await PackageDownloader.UninstallPackageAsync(_pkg, _appRoot);
                _onChanged();
            }
        };

        Controls.Add(_pill);
        Controls.Add(_lblTitle);
        Controls.Add(_lblTag);
        Controls.Add(_lblStatus);
        Controls.Add(_btnAction);
        Controls.Add(_btnSwitch);
        Controls.Add(_btnDelete);

        Resize += (s, e) => LayoutRowControls();
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        bool installed = _pkg.IsInstalled(_appRoot);
        bool active = _pkg.IsActive(_config);

        if (_isDownloading)
        {
            _pill.Status = ServiceStatus.Starting;
            _btnAction.Text = "Cancel";
            _btnAction.IconKind = IconKind.Stop;
            _btnAction.ShowBorder = true;
            _btnAction.NormalColor = ModernColors.Card;
            _btnAction.HoverColor = ModernColors.DangerBg;
            _btnAction.ForeColor = ModernColors.Danger;
            _btnSwitch.Visible = false;
            _btnDelete.Visible = false;
            _lblStatus.Visible = true;
            _lblTag.Visible = false;
        }
        else if (installed)
        {
            _pill.Status = active ? ServiceStatus.Running : ServiceStatus.Stopped;
            _btnAction.Visible = false;
            _btnSwitch.Visible = true;
            _btnSwitch.Text = active ? "Active" : "Use";
            _btnSwitch.IconKind = active ? IconKind.Check : IconKind.Play;
            _btnSwitch.ForeColor = active ? ModernColors.Success : ModernColors.TextPrimary;
            _btnSwitch.NormalColor = active ? Color.FromArgb(16, 40, 32) : ModernColors.Card;
            _btnDelete.Visible = !active;
            _lblStatus.Visible = false;
            _lblTag.Visible = true;
        }
        else
        {
            _pill.Status = ServiceStatus.NotInstalled;
            _btnAction.Visible = true;
            _btnAction.Text = "Install";
            _btnAction.IconKind = IconKind.Download;
            _btnAction.ShowBorder = false;
            _btnAction.NormalColor = ModernColors.Primary;
            _btnAction.HoverColor = ModernColors.PrimaryHover;
            _btnAction.ForeColor = Color.White;
            _btnSwitch.Visible = false;
            _btnDelete.Visible = false;
            _lblStatus.Visible = false;
            _lblTag.Visible = true;
        }

        LayoutRowControls();
        Invalidate();
    }

    private void LayoutRowControls()
    {
        int cardH = Height - 8;
        int rightX = Width > 150 ? Width - 14 : 450;

        // Delete button
        if (_btnDelete.Visible)
        {
            _btnDelete.Location = new Point(rightX - _btnDelete.Width, (cardH - _btnDelete.Height) / 2);
            rightX = _btnDelete.Left - 8;
        }

        // Switch button
        if (_btnSwitch.Visible)
        {
            _btnSwitch.Location = new Point(rightX - _btnSwitch.Width, (cardH - _btnSwitch.Height) / 2);
            rightX = _btnSwitch.Left - 8;
        }

        // Action button (Install / Cancel)
        if (_btnAction.Visible)
        {
            _btnAction.Location = new Point(rightX - _btnAction.Width, (cardH - _btnAction.Height) / 2);
            rightX = _btnAction.Left - 8;
        }
    }

    private async Task StartDownloadAsync()
    {
        _isDownloading = true;
        _cts = new CancellationTokenSource();
        UpdateVisuals();

        var progress = new Progress<PackageDownloadProgress>(p =>
        {
            if (InvokeRequired)
            {
                Invoke(() => ApplyProgress(p));
            }
            else
            {
                ApplyProgress(p);
            }
        });

        bool success = await PackageDownloader.DownloadAndInstallAsync(_pkg, _appRoot, progress, _cts.Token);
        _isDownloading = false;

        if (success)
        {
            _onChanged();
        }
        else
        {
            UpdateVisuals();
        }
    }

    private void ApplyProgress(PackageDownloadProgress p)
    {
        _lblStatus.Text = p.StatusText;

        if (p.HasError)
        {
            _lblStatus.ForeColor = ModernColors.Danger;
        }
        else if (p.IsCompleted)
        {
            _lblStatus.ForeColor = ModernColors.Success;
        }
        else
        {
            _lblStatus.ForeColor = ModernColors.TextSecondary;
        }
    }

    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
