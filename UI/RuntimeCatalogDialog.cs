using System.Drawing.Drawing2D;
using DevLiteServer.Core;
using DevLiteServer.Core.Downloader;

namespace DevLiteServer.UI;

public class RuntimeCatalogDialog : Form
{
    private readonly string _category;
    private readonly string _appRoot;
    private readonly AppConfig _config;
    private readonly Func<PackageItem, Task>? _onInstalledAsync;

    private readonly Panel _pnlList;
    private readonly ModernButton _btnClose;
    private CancellationTokenSource? _activeCts;
    private bool _isBusy = false;

    public bool HasInstalledAny { get; private set; }

    public RuntimeCatalogDialog(string category, string appRoot, AppConfig config, Func<PackageItem, Task>? onInstalledAsync = null)
    {
        _category = category;
        _appRoot = appRoot;
        _config = config;
        _onInstalledAsync = onInstalledAsync;

        Text = $"Install {_category} Runtime";
        ClientSize = new Size(580, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;

        // 1. Header Panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = ModernColors.Surface,
            Padding = new Padding(22, 12, 22, 10)
        };
        headerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = $"Available {_category} Runtimes",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 12)
        };

        var lblSubtitle = new Label
        {
            Text = $"Select and install official {_category} packages directly from official mirrors.",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(20, 36)
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSubtitle);

        // 2. Footer Panel
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = ModernColors.Surface,
            Padding = new Padding(20, 10, 20, 10)
        };
        footerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
        };

        _btnClose = new ModernButton
        {
            Text = "Done",
            Width = 95,
            Height = 34,
            Location = new Point(ClientSize.Width - 115, 11),
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            ForeColor = Color.White,
            ShowBorder = false
        };
        _btnClose.Click += (s, e) =>
        {
            if (_isBusy)
            {
                var res = MessageBox.Show(this, "A download is currently in progress. Cancel download and close?", "Confirm Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (res != DialogResult.Yes) return;
                _activeCts?.Cancel();
            }
            DialogResult = DialogResult.OK;
            Close();
        };

        footerPanel.Controls.Add(_btnClose);

        // 3. Scrollable List Panel
        _pnlList = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20, 14, 20, 14),
            BackColor = ModernColors.Background
        };

        Controls.Add(_pnlList);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);

        LoadPackages();
    }

    private async void LoadPackages()
    {
        _pnlList.Controls.Clear();

        var lblLoading = new Label
        {
            Text = "Loading available packages...",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 9.5f),
            AutoSize = true,
            Location = new Point(24, 20)
        };
        _pnlList.Controls.Add(lblLoading);

        List<PackageItem> packages;
        try
        {
            if (_category.Equals("PHP", StringComparison.OrdinalIgnoreCase))
            {
                await PackageCatalog.RefreshPhpReleasesAsync();
                packages = PackageCatalog.GetPhpPackages();
            }
            else
            {
                packages = PackageCatalog.GetNodePackages();
            }
        }
        catch
        {
            packages = _category.Equals("PHP", StringComparison.OrdinalIgnoreCase)
                ? PackageCatalog.GetPhpPackages()
                : PackageCatalog.GetNodePackages();
        }

        _pnlList.Controls.Clear();

        if (packages.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "No packages available.",
                ForeColor = ModernColors.TextMuted,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true,
                Location = new Point(24, 20)
            };
            _pnlList.Controls.Add(lblEmpty);
            return;
        }

        int top = 10;
        foreach (var pkg in packages)
        {
            var card = CreatePackageCatalogCard(pkg);
            card.Location = new Point(20, top);
            card.Width = _pnlList.ClientSize.Width - 40;
            card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _pnlList.Controls.Add(card);
            top += card.Height + 10;
        }
    }

    private Panel CreatePackageCatalogCard(PackageItem pkg)
    {
        bool installed = pkg.IsInstalled(_appRoot);

        var card = new Panel
        {
            Height = 68,
            BackColor = ModernColors.Card,
            Padding = new Padding(16, 10, 16, 10)
        };

        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, card.Width - 1f, card.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bgBrush = new SolidBrush(ModernColors.Card);
            g.FillPath(bgBrush, path);

            using var borderPen = new Pen(installed ? Color.FromArgb(120, ModernColors.Success) : ModernColors.BorderSubtle, 1f);
            g.DrawPath(borderPen, path);
        };

        var lblName = new Label
        {
            Text = pkg.Name,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(14, 12)
        };

        double mb = pkg.ApproximateSizeBytes / (1024.0 * 1024.0);
        string sizeText = mb > 0 ? $" • {mb:F1} MB" : "";
        var lblTag = new Label
        {
            Text = $"{pkg.Tag}{sizeText}",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(14, 35)
        };

        var pbar = new ProgressBar
        {
            Width = 150,
            Height = 12,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visible = false,
            Location = new Point(card.Width - 170, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        var lblStatus = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8f),
            ForeColor = ModernColors.TextMuted,
            AutoSize = true,
            Visible = false,
            Location = new Point(card.Width - 170, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        var btnInstall = new ModernButton
        {
            Text = installed ? "Installed" : "Install",
            Width = 90,
            Height = 32,
            Location = new Point(card.Width - 105, 18),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            NormalColor = installed ? ModernColors.SuccessBg : ModernColors.Primary,
            HoverColor = installed ? ModernColors.SuccessBg : ModernColors.PrimaryHover,
            ForeColor = installed ? ModernColors.Success : Color.White,
            ShowBorder = false,
            Enabled = !installed
        };

        btnInstall.Click += async (s, e) =>
        {
            if (installed || _isBusy) return;

            _isBusy = true;
            _activeCts = new CancellationTokenSource();
            btnInstall.Visible = false;
            pbar.Visible = true;
            lblStatus.Visible = true;
            lblStatus.Text = "Starting...";

            var progress = new Progress<PackageDownloadProgress>(p =>
            {
                if (IsDisposed) return;
                Invoke(() =>
                {
                    pbar.Value = Math.Clamp((int)p.Percent, 0, 100);
                    lblStatus.Text = $"{p.Percent:F0}%";
                });
            });

            try
            {
                bool ok = await PackageDownloader.DownloadAndInstallAsync(pkg, _appRoot, progress, _activeCts.Token);
                if (ok)
                {
                    HasInstalledAny = true;
                    installed = true;
                    pbar.Visible = false;
                    lblStatus.Visible = false;
                    btnInstall.Text = "Installed";
                    btnInstall.NormalColor = ModernColors.SuccessBg;
                    btnInstall.HoverColor = ModernColors.SuccessBg;
                    btnInstall.ForeColor = ModernColors.Success;
                    btnInstall.Enabled = false;
                    btnInstall.Visible = true;
                    card.Invalidate();

                    if (_onInstalledAsync != null)
                    {
                        await _onInstalledAsync(pkg);
                    }
                }
                else
                {
                    pbar.Visible = false;
                    lblStatus.Visible = false;
                    btnInstall.Visible = true;
                    btnInstall.Text = "Retry";
                    MessageBox.Show(this, $"Failed to install {pkg.Name}.", "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                pbar.Visible = false;
                lblStatus.Visible = false;
                btnInstall.Visible = true;
                btnInstall.Text = "Install";
            }
            catch (Exception ex)
            {
                pbar.Visible = false;
                lblStatus.Visible = false;
                btnInstall.Visible = true;
                btnInstall.Text = "Retry";
                MessageBox.Show(this, $"Error: {ex.Message}", "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isBusy = false;
                _activeCts?.Dispose();
                _activeCts = null;
            }
        };

        card.Controls.Add(lblName);
        card.Controls.Add(lblTag);
        card.Controls.Add(pbar);
        card.Controls.Add(lblStatus);
        card.Controls.Add(btnInstall);

        return card;
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
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
