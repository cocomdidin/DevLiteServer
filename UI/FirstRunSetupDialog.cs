using System.Drawing.Drawing2D;
using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class FirstRunSetupDialog : Form
{
    private readonly string _appRoot;
    private readonly AppConfig _config;
    private readonly List<CoreComponentStatus> _components;
    private readonly List<(CoreComponentStatus Status, Label LblBadge, Label LblDesc, Panel RowPanel)> _rows = [];

    private readonly ProgressBar _progressBar;
    private readonly Label _lblProgress;
    private readonly CheckBox _chkRegisterPath;
    private readonly ModernButton _btnInstall;
    private readonly ModernButton _btnSkip;
    private CancellationTokenSource? _cts;
    private bool _isInstalling = false;
    private bool _isCompleted = false;

    public FirstRunSetupDialog(string appRoot, AppConfig config)
    {
        _appRoot = appRoot;
        _config = config;
        _components = CoreProvisioner.CheckComponents(appRoot);

        Text = "Dev Lite Server - Core Setup";
        ClientSize = new Size(620, 590);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;

        // 1. Header
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = ModernColors.Surface
        };
        headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var badgeRect = new RectangleF(20, 16, 38, 38);
            using var badgeBg = new SolidBrush(Color.FromArgb(12, 74, 110));
            using var badgeBorder = new Pen(Color.FromArgb(56, 189, 248, 140), 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(29, 25, 20, 20);
            VectorIcons.Draw(g, IconKind.Lightning, iconRect, ModernColors.Primary);

            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "Welcome to Dev Lite Server",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(68, 14),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "First-run auto-setup: provision core web runtime and tools in 1-click.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(70, 40),
            AutoSize = true
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSub);

        // 2. Component Checklist Cards Container
        var pnlContainer = new Panel
        {
            Location = new Point(18, 82),
            Size = new Size(584, 370),
            BackColor = Color.Transparent,
            AutoScroll = true
        };

        int top = 0;
        int rowWidth = pnlContainer.Width - (SystemInformation.VerticalScrollBarWidth + 4);
        foreach (var comp in _components)
        {
            var row = CreateComponentRow(comp, top, rowWidth);
            pnlContainer.Controls.Add(row.RowPanel);
            _rows.Add(row);
            top += 62;
        }

        // 3. Footer with Progress, Path Checkbox & Buttons
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 128,
            BackColor = ModernColors.Surface
        };
        footerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
        };

        _lblProgress = new Label
        {
            Text = "Click \"Install Core Components\" to automatically download and initialize your environment.",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(20, 10),
            Size = new Size(580, 18),
            AutoEllipsis = true
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 32),
            Size = new Size(580, 8),
            Style = ProgressBarStyle.Continuous,
            Value = 0,
            Visible = false
        };

        _chkRegisterPath = new CheckBox
        {
            Text = "Register PHP, Composer, and Git to Windows User PATH (Recommended)",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(20, 52),
            Size = new Size(580, 24),
            Checked = true,
            Cursor = Cursors.Hand
        };

        _btnSkip = new ModernButton
        {
            Text = "Skip For Now",
            Location = new Point(280, 82),
            Size = new Size(110, 36),
            NormalColor = Color.FromArgb(31, 41, 55),
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            BorderLineColor = ModernColors.Border,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Cursor = Cursors.Hand
        };
        _btnSkip.Click += (s, e) =>
        {
            if (_isInstalling) return;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnInstall = new ModernButton
        {
            Text = "Install Core Components (1-Click)",
            Location = new Point(400, 82),
            Size = new Size(200, 36),
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            ForeColor = Color.Black,
            BorderLineColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnInstall.Click += async (s, e) =>
        {
            if (_isCompleted)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            if (_isInstalling) return;
            await StartProvisioningAsync();
        };

        footerPanel.Controls.Add(_lblProgress);
        footerPanel.Controls.Add(_progressBar);
        footerPanel.Controls.Add(_chkRegisterPath);
        footerPanel.Controls.Add(_btnSkip);
        footerPanel.Controls.Add(_btnInstall);

        Controls.Add(pnlContainer);
        Controls.Add(headerPanel);
        Controls.Add(footerPanel);

        FormClosing += (s, e) =>
        {
            if (_isInstalling)
            {
                var result = MessageBox.Show(
                    "Core components installation is in progress. Are you sure you want to cancel?",
                    "Cancel Setup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                _cts?.Cancel();
            }
        };
    }

    private (CoreComponentStatus Status, Label LblBadge, Label LblDesc, Panel RowPanel) CreateComponentRow(
        CoreComponentStatus comp, int top, int width)
    {
        var panel = new Panel
        {
            Location = new Point(0, top),
            Size = new Size(width, 56),
            BackColor = ModernColors.Card,
            Cursor = Cursors.Default
        };

        panel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Border
            var rect = new RectangleF(0.5f, 0.5f, panel.Width - 1, panel.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var borderPen = new Pen(comp.IsInstalled ? Color.FromArgb(20, 83, 45) : ModernColors.BorderSubtle, 1);
            g.DrawPath(borderPen, path);

            // Icon circle on left
            var iconCircle = new RectangleF(12, 12, 32, 32);
            Color circleBg = comp.IsInstalled ? Color.FromArgb(20, 83, 45) : Color.FromArgb(30, 41, 59);
            Color iconColor = comp.IsInstalled ? ModernColors.Success : ModernColors.TextSecondary;
            IconKind icon = comp.IsInstalled ? IconKind.Check : IconKind.Download;

            using var circleBrush = new SolidBrush(circleBg);
            g.FillEllipse(circleBrush, iconCircle);

            var iconInner = new RectangleF(iconCircle.X + 7, iconCircle.Y + 7, 18, 18);
            VectorIcons.Draw(g, icon, iconInner, iconColor);
        };

        var lblName = new Label
        {
            Text = $"{comp.Name} ({comp.ApproximateSize})",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(52, 9),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = comp.Description,
            Font = new Font("Segoe UI", 8f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(53, 29),
            Size = new Size(390, 18),
            AutoEllipsis = true
        };

        var lblBadge = new Label
        {
            Text = comp.IsInstalled ? "INSTALLED" : "PENDING",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = comp.IsInstalled ? ModernColors.Success : ModernColors.TextMuted,
            BackColor = comp.IsInstalled ? Color.FromArgb(20, 83, 45) : Color.FromArgb(30, 41, 59),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(panel.Width - 105, 16),
            Size = new Size(92, 24)
        };
        lblBadge.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, lblBadge.Width - 1, lblBadge.Height - 1);
            using var p = CreateRoundedRectangle(r, 4f);
            using var borderPen = new Pen(comp.IsInstalled ? ModernColors.Success : ModernColors.Border, 1);
            g.DrawPath(borderPen, p);
        };

        panel.Controls.Add(lblName);
        panel.Controls.Add(lblDesc);
        panel.Controls.Add(lblBadge);

        return (comp, lblBadge, lblDesc, panel);
    }

    private async Task StartProvisioningAsync()
    {
        _isInstalling = true;
        _btnInstall.Enabled = false;
        _btnSkip.Enabled = false;
        _chkRegisterPath.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Value = 0;
        _cts = new CancellationTokenSource();

        var missing = _rows.Where(r => !r.Status.IsInstalled).ToList();
        if (missing.Count == 0)
        {
            FinishSetup(true);
            return;
        }

        int totalToInstall = missing.Count;
        int completedCount = 0;

        foreach (var row in missing)
        {
            if (_cts.IsCancellationRequested) break;

            var comp = row.Status;
            row.LblBadge.Text = "DOWNLOADING";
            row.LblBadge.ForeColor = ModernColors.Primary;
            row.LblBadge.BackColor = Color.FromArgb(12, 74, 110);
            row.RowPanel.Invalidate();

            var progress = new Progress<(double Percent, string Status)>(p =>
            {
                if (IsDisposed) return;
                int basePercent = (int)((completedCount / (double)totalToInstall) * 100.0);
                int subPercent = (int)((p.Percent / 100.0) * (100.0 / totalToInstall));
                _progressBar.Value = Math.Clamp(basePercent + subPercent, 0, 100);

                _lblProgress.Text = $"[{completedCount + 1}/{totalToInstall}] {comp.Name}: {p.Status}";
                row.LblDesc.Text = p.Status;
            });

            bool success = await Task.Run(() => CoreProvisioner.InstallComponentAsync(comp.Kind, _appRoot, progress, _cts.Token));

            if (success)
            {
                comp.IsInstalled = true;
                row.LblBadge.Text = "READY";
                row.LblBadge.ForeColor = ModernColors.Success;
                row.LblBadge.BackColor = Color.FromArgb(20, 83, 45);
                row.LblDesc.Text = comp.Description;
                row.RowPanel.Invalidate();
                completedCount++;
            }
            else
            {
                row.LblBadge.Text = "FAILED";
                row.LblBadge.ForeColor = ModernColors.Danger;
                row.LblBadge.BackColor = Color.FromArgb(136, 19, 55);
                row.RowPanel.Invalidate();
                _lblProgress.Text = $"Failed to install {comp.Name}. Check your internet connection.";
                break;
            }
        }

        _isInstalling = false;
        if (completedCount == totalToInstall)
        {
            FinishSetup(true);
        }
        else
        {
            _btnInstall.Enabled = true;
            _btnInstall.Text = "Retry Setup";
            _btnSkip.Enabled = true;
            _chkRegisterPath.Enabled = true;
        }
    }

    private void FinishSetup(bool allOk)
    {
        _isCompleted = true;
        _progressBar.Value = 100;
        _lblProgress.Text = "All core components are installed and configured!";
        _lblProgress.ForeColor = ModernColors.Success;

        // Ensure starter www/index.php
        CoreProvisioner.EnsureStarterWebRoot(_appRoot);

        // Register User PATH if checked
        if (_chkRegisterPath.Checked)
        {
            UserEnvironmentManager.SetRegistration(_appRoot, true);
            _config.RegisterUserPath = true;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        }

        _btnInstall.Text = "Get Started";
        _btnInstall.NormalColor = ModernColors.Success;
        _btnInstall.HoverColor = ModernColors.SuccessHover;
        _btnInstall.BorderLineColor = ModernColors.Success;
        _btnInstall.ForeColor = Color.White;
        _btnInstall.Enabled = true;

        _btnSkip.Visible = false;
        _btnInstall.Location = new Point(440, 82);
        _btnInstall.Size = new Size(160, 36);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2f;
        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
