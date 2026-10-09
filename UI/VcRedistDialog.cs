using System.Drawing.Drawing2D;
using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class VcRedistDialog : Form
{
    private readonly ProgressBar _progressBar;
    private readonly Label _lblProgress;
    private readonly ModernButton _btnInstall;
    private readonly ModernButton _btnSkip;
    private bool _isInstalling = false;

    public VcRedistDialog()
    {
        Text = "Visual C++ Runtime Required";
        ClientSize = new Size(500, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ModernColors.Surface;
        ForeColor = ModernColors.TextPrimary;

        // 1. Header with Warning/Shield Icon
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(20, 14, 20, 10),
            BackColor = Color.FromArgb(20, 28, 44)
        };
        headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Shield/Warning badge
            var badgeRect = new RectangleF(20, 16, 36, 36);
            using var badgeBg = new SolidBrush(Color.FromArgb(45, 30, 10));
            using var badgeBorder = new Pen(Color.FromArgb(245, 158, 11, 160), 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(28, 24, 20, 20);
            VectorIcons.Draw(g, IconKind.Warning, iconRect, ModernColors.Warning);

            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "Microsoft Visual C++ Runtime Missing",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(68, 14),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "VCRUNTIME140.dll (Visual C++ 2015-2022 x64) is required.",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(69, 38),
            AutoSize = true
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSub);

        // 2. Info Card
        var bodyCard = new Panel
        {
            Location = new Point(20, 88),
            Size = new Size(460, 150),
            BackColor = ModernColors.Card,
            Padding = new Padding(16, 12, 16, 12)
        };
        bodyCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float half = stroke / 2f;
            var rect = new RectangleF(half, half, bodyCard.Width - stroke, bodyCard.Height - stroke);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var pen = new Pen(ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);
        };

        var lblDescription = new Label
        {
            Text = "PHP FastCGI and relational database engines depend on MSVCRT x64.\nWithout it, services will crash silently when started.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(16, 12),
            Size = new Size(428, 36)
        };

        var lblSource = new Label
        {
            Text = "Source: Official Microsoft Installer (vc_redist.x64.exe)",
            Font = new Font("Segoe UI", 8f, FontStyle.Italic),
            ForeColor = ModernColors.Primary,
            Location = new Point(16, 54),
            AutoSize = true
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(16, 82),
            Size = new Size(428, 18),
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visible = false
        };

        _lblProgress = new Label
        {
            Text = "Click 'Download & Install' to install automatically.",
            Font = new Font("Segoe UI", 8f),
            ForeColor = ModernColors.TextMuted,
            Location = new Point(16, 108),
            Size = new Size(428, 20)
        };

        bodyCard.Controls.Add(lblDescription);
        bodyCard.Controls.Add(lblSource);
        bodyCard.Controls.Add(_progressBar);
        bodyCard.Controls.Add(_lblProgress);

        // 3. Action Buttons
        _btnInstall = new ModernButton
        {
            Text = "Download & Install Now",
            IconKind = IconKind.Lightning,
            IconSize = 11,
            Width = 175,
            Height = 34,
            Location = new Point(305, 256),
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            PressedColor = Color.FromArgb(2, 132, 199),
            BorderRadius = 6,
            ShowBorder = false,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        _btnInstall.Click += async (s, e) => await StartInstallationAsync();

        _btnSkip = new ModernButton
        {
            Text = "Skip for Now",
            Width = 100,
            Height = 34,
            Location = new Point(195, 256),
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            PressedColor = ModernColors.Background,
            BorderRadius = 6,
            ShowBorder = true,
            BorderLineColor = ModernColors.BorderSubtle,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        _btnSkip.Click += (s, e) =>
        {
            DialogResult = DialogResult.Ignore;
            Close();
        };

        Controls.Add(headerPanel);
        Controls.Add(bodyCard);
        Controls.Add(_btnInstall);
        Controls.Add(_btnSkip);

        AcceptButton = _btnInstall;
        CancelButton = _btnSkip;
    }

    private async Task StartInstallationAsync()
    {
        if (_isInstalling) return;
        _isInstalling = true;

        _btnInstall.Enabled = false;
        _btnSkip.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Value = 0;
        _lblProgress.Text = "Downloading official installer from Microsoft...";
        _lblProgress.ForeColor = ModernColors.Primary;

        var progress = new Progress<int>(pct =>
        {
            _progressBar.Value = Math.Clamp(pct, 0, 100);
            _lblProgress.Text = $"Downloading: {pct}%";
        });

        bool success = await Task.Run(async () => await DependencyChecker.InstallVcRedistAsync(progress));

        if (success || DependencyChecker.IsVcRedistInstalled())
        {
            _progressBar.Value = 100;
            _lblProgress.Text = "✓ Visual C++ Redistributable installed successfully!";
            _lblProgress.ForeColor = ModernColors.Success;
            await Task.Delay(1000);
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _progressBar.Visible = false;
            _lblProgress.Text = "Installation was cancelled or encountered an error.";
            _lblProgress.ForeColor = ModernColors.Danger;
            _btnInstall.Enabled = true;
            _btnSkip.Enabled = true;
            _isInstalling = false;
        }
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
