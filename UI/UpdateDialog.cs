using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class UpdateDialog : Form
{
    private readonly UpdateInfo _info;
    private readonly Func<Task> _beforeInstallShutdown;

    private readonly Label _lblTitle;
    private readonly Label _lblSub;
    private readonly Label _lblVersionBadges;
    private readonly TextBox _txtNotes;
    private readonly ProgressBar _progressBar;
    private readonly Label _lblStatus;

    private readonly ModernButton _btnInstall;
    private readonly ModernButton _btnViewOnWeb;
    private readonly ModernButton _btnClose;

    private CancellationTokenSource? _downloadCts;

    public UpdateDialog(UpdateInfo info, Func<Task> beforeInstallShutdown)
    {
        _info = info;
        _beforeInstallShutdown = beforeInstallShutdown;

        Text = "Dev Lite Server Update";
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch { }
        Size = new Size(540, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = ModernColors.Surface,
            Padding = new Padding(24, 14, 24, 10)
        };

        _lblTitle = new Label
        {
            Text = _info.IsUpdateAvailable ? "🚀 New Version Available" : "✔ You're Up to Date",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = _info.IsUpdateAvailable ? ModernColors.Primary : ModernColors.Success,
            AutoSize = true,
            Location = new Point(24, 12)
        };

        _lblSub = new Label
        {
            Text = _info.IsUpdateAvailable
                ? $"Dev Lite Server {_info.ReleaseName} is ready to install."
                : $"You are running the latest version (v{_info.CurrentVersion}).",
            Font = new Font("Segoe UI", 9f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(26, 40)
        };

        headerPanel.Controls.Add(_lblTitle);
        headerPanel.Controls.Add(_lblSub);

        _lblVersionBadges = new Label
        {
            Text = _info.IsUpdateAvailable
                ? $"Current: v{_info.CurrentVersion}   ➔   Latest: v{_info.LatestVersion}"
                : $"Installed Version: v{_info.CurrentVersion}",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(24, 85),
            AutoSize = true
        };

        var lblNotesHeader = new Label
        {
            Text = "Release Notes:",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextMuted,
            Location = new Point(24, 115),
            AutoSize = true
        };

        _txtNotes = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(24, 138),
            Size = new Size(476, 175),
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextSecondary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
            Text = string.IsNullOrWhiteSpace(_info.ReleaseNotes) ? "No detailed release notes provided." : _info.ReleaseNotes.Replace("\n", "\r\n")
        };

        _lblStatus = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(24, 322),
            Size = new Size(476, 20),
            Visible = false
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(24, 346),
            Size = new Size(476, 18),
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Visible = false
        };

        // Footer buttons
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = ModernColors.Surface,
            Padding = new Padding(24, 12, 24, 12)
        };

        _btnInstall = new ModernButton
        {
            Text = "Install Update",
            IconKind = IconKind.Refresh,
            IconSize = 12,
            Width = 135,
            Height = 34,
            Location = new Point(365, 13),
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            PressedColor = ModernColors.SuccessBg,
            BorderRadius = 6,
            ShowBorder = false,
            Visible = _info.IsUpdateAvailable && !string.IsNullOrEmpty(_info.DownloadUrl)
        };
        _btnInstall.Click += async (s, e) => await StartDownloadAndInstallAsync();

        _btnViewOnWeb = new ModernButton
        {
            Text = "View on GitHub",
            IconKind = IconKind.Globe,
            IconSize = 12,
            Width = 135,
            Height = 34,
            Location = new Point(220, 13),
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            PressedColor = ModernColors.Background,
            BorderRadius = 6,
            Visible = !string.IsNullOrEmpty(_info.HtmlUrl)
        };
        _btnViewOnWeb.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_info.HtmlUrl))
            {
                Process.Start(new ProcessStartInfo(_info.HtmlUrl) { UseShellExecute = true });
            }
        };

        _btnClose = new ModernButton
        {
            Text = _info.IsUpdateAvailable ? "Later" : "Close",
            Width = 90,
            Height = 34,
            Location = new Point(24, 13),
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            PressedColor = ModernColors.Background,
            BorderRadius = 6
        };
        _btnClose.Click += (s, e) =>
        {
            _downloadCts?.Cancel();
            Close();
        };

        footerPanel.Controls.Add(_btnInstall);
        footerPanel.Controls.Add(_btnViewOnWeb);
        footerPanel.Controls.Add(_btnClose);

        Controls.Add(headerPanel);
        Controls.Add(_lblVersionBadges);
        Controls.Add(lblNotesHeader);
        Controls.Add(_txtNotes);
        Controls.Add(_lblStatus);
        Controls.Add(_progressBar);
        Controls.Add(footerPanel);
    }

    private async Task StartDownloadAndInstallAsync()
    {
        if (string.IsNullOrEmpty(_info.DownloadUrl)) return;

        _btnInstall.Enabled = false;
        _btnViewOnWeb.Enabled = false;
        _btnClose.Text = "Cancel";

        _lblStatus.Visible = true;
        _lblStatus.Text = "Connecting to GitHub...";
        _lblStatus.ForeColor = ModernColors.TextSecondary;

        _progressBar.Visible = true;
        _progressBar.Value = 0;

        _downloadCts = new CancellationTokenSource();

        string tempInstaller = Path.Combine(Path.GetTempPath(), $"DevLiteServer-Setup-v{_info.LatestVersion}.exe");

        var progress = new Progress<int>(pct =>
        {
            if (!IsDisposed && _progressBar.IsHandleCreated)
            {
                _progressBar.Value = Math.Clamp(pct, 0, 100);
            }
        });

        var statusProgress = new Progress<string>(text =>
        {
            if (!IsDisposed && _lblStatus.IsHandleCreated)
            {
                _lblStatus.Text = text;
            }
        });

        bool success = await Task.Run(async () =>
            await UpdateChecker.DownloadInstallerAsync(
                _info.DownloadUrl,
                tempInstaller,
                progress,
                statusProgress,
                _downloadCts.Token
            )
        );

        if (!success)
        {
            if (_downloadCts.IsCancellationRequested)
            {
                _lblStatus.Text = "Download canceled.";
            }
            else
            {
                _lblStatus.Text = "❌ Failed to download update. Please try again or download manually.";
                _lblStatus.ForeColor = ModernColors.Danger;
            }
            _btnInstall.Enabled = true;
            _btnViewOnWeb.Enabled = true;
            _btnClose.Text = "Close";
            return;
        }

        _lblStatus.Text = "✔ Download complete. Preparing to launch installer...";
        _lblStatus.ForeColor = ModernColors.Success;
        await Task.Delay(800);

        var confirm = MessageBox.Show(
            this,
            "Update installer is ready.\n\nDev Lite Server will now close running daemons and launch the update setup wizard.\n\nDo you want to proceed?",
            "Confirm Update Installation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (confirm != DialogResult.Yes)
        {
            _btnInstall.Enabled = true;
            _btnViewOnWeb.Enabled = true;
            _btnClose.Text = "Close";
            return;
        }

        try
        {
            // Cleanly stop all services first
            await _beforeInstallShutdown();

            // Run downloaded installer
            Process.Start(new ProcessStartInfo
            {
                FileName = tempInstaller,
                UseShellExecute = true
            });

            // Exit application to allow installer to overwrite binaries
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error launching installer: {ex.Message}", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnInstall.Enabled = true;
            _btnViewOnWeb.Enabled = true;
            _btnClose.Text = "Close";
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _downloadCts?.Cancel();
        base.OnFormClosing(e);
    }
}
