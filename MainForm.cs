using System.Diagnostics;
using DevLiteServer.Core;
using DevLiteServer.Services;
using DevLiteServer.UI;

namespace DevLiteServer;

public partial class MainForm : Form
{
    private readonly string _appRoot;
    private readonly JobObject _job;
    private readonly AppConfig _config;

    private readonly NginxService _nginx;
    private readonly PhpService _php;
    private readonly MySqlService _mysql;
    private readonly MailpitService _mailpit;
    private readonly PostgreSqlService _postgresql;
    private readonly RedisService _redis;
    private readonly List<IService> _services = new();

    private readonly bool _startMinimized;
    private bool _hasBeenShown = false;
    private bool _isExitingExplicitly = false;

    public MainForm(bool startMinimized = false)
    {
        _startMinimized = startMinimized;
        InitializeComponent();

        _appRoot = AppPaths.ResolveRoot();

        string iniPath = Path.Combine(_appRoot, "config.ini");
        _config = ConfigManager.Load(iniPath);

        // Sync Windows Logon Startup state with registry
        if (_config.StartWithWindows != WindowsStartup.IsEnabled())
        {
            WindowsStartup.SetStartup(_config.StartWithWindows, Application.ExecutablePath);
        }

        _job = new JobObject();

        _php = new PhpService(_appRoot, _job, _config);
        _nginx = new NginxService(_appRoot, _job, _config, _php);
        _mysql = new MySqlService(_appRoot, _job, _config);
        _mailpit = new MailpitService(_appRoot, _job, _config);
        _postgresql = new PostgreSqlService(_appRoot, _job, _config);
        _redis = new RedisService(_appRoot, _job, _config);

        _services.AddRange([_php, _nginx, _mysql, _mailpit, _postgresql, _redis]);

        foreach (var svc in _services)
        {
            svc.StatusChanged += (s, status) =>
            {
                if (InvokeRequired)
                {
                    Invoke(() => OnServiceStatusChanged(s, status));
                }
                else
                {
                    OnServiceStatusChanged(s, status);
                }
            };
        }

        BuildServiceCards();
        BuildTrayMenu();
        UpdateServiceStats();

        // Check for Visual C++ runtime
        if (!DependencyChecker.IsVcRedistInstalled())
        {
            lblStatusText.Text = "⚠ Warning: Visual C++ Redistributable (x64) is missing. PHP might fail to start.";
            lblStatusText.ForeColor = ModernColors.Warning;
        }
        else
        {
            lblStatusText.Text = $"Ready | Root: {_appRoot}";
            lblStatusText.ForeColor = ModernColors.TextSecondary;
        }

        try
        {
            string iconFile = Path.Combine(_appRoot, "assets", "app.ico");
            Icon appIcon = File.Exists(iconFile)
                ? new Icon(iconFile)
                : (Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application);

            notifyIcon.Icon = appIcon;
            Icon = appIcon;
        }
        catch
        {
            notifyIcon.Icon = SystemIcons.Application;
            Icon = SystemIcons.Application;
        }

        // Wire service status updates to toggle Adminer button
        _nginx.StatusChanged += (_, _) => UpdateAdminerState();
        _php.StatusChanged += (_, _) => UpdateAdminerState();
        UpdateAdminerState();

        Shown += async (s, e) =>
        {
            if (_config.AutoStartServices)
            {
                await AutoStartConfiguredServicesAsync();
            }

            if (_config.CheckUpdatesOnStart)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    await CheckForUpdatesAsync(manual: false);
                });
            }
        };
    }

    private void OnServiceStatusChanged(IService svc, ServiceStatus status)
    {
        if (status == ServiceStatus.Error && !string.IsNullOrEmpty(svc.LastError))
        {
            lblStatusText.Text = $"❌ [{svc.Name}] {svc.LastError}";
            lblStatusText.ForeColor = ModernColors.Danger;
        }
        else if (status == ServiceStatus.Running)
        {
            lblStatusText.Text = $"✔ [{svc.Name}] running on port {svc.Port}";
            lblStatusText.ForeColor = ModernColors.Success;
        }

        UpdateServiceStats();
    }

    private void UpdateServiceStats()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateServiceStats);
            return;
        }

        int enabledCount = _services.Count(s => _config.IsServiceEnabled(s.Name));
        int runningCount = _services.Count(s => _config.IsServiceEnabled(s.Name) && s.Status == ServiceStatus.Running);

        lblStatsText.Text = $"Services: {runningCount} / {enabledCount} running";
        lblStatsText.ForeColor = runningCount > 0 ? ModernColors.Success : ModernColors.TextMuted;
        lblStatsText.Location = new Point(footerPanel.ClientSize.Width - lblStatsText.Width - 20, 8);
    }

    private void UpdateAdminerState()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateAdminerState);
            return;
        }
        btnOpenAdminer.Enabled = _nginx.Status == ServiceStatus.Running && _php.Status == ServiceStatus.Running;
    }

    private void LayoutActionButtons()
    {
        if (btnOpenWww == null || btnSettings == null || btnCheckUpdates == null) return;

        int left = 20;
        const int spacing = 8;
        const int top = 7;

        btnOpenWww.Location = new Point(left, top);
        left += btnOpenWww.Width + spacing;

        btnOpenTerminal.Location = new Point(left, top);
        left += btnOpenTerminal.Width + spacing;

        if (_config.EnableMailpit)
        {
            btnOpenMailpit.Visible = true;
            btnOpenMailpit.Location = new Point(left, top);
            left += btnOpenMailpit.Width + spacing;
        }
        else
        {
            btnOpenMailpit.Visible = false;
        }

        btnOpenAdminer.Location = new Point(left, top);

        int panelWidth = actionsPanel.Width > 200 ? actionsPanel.Width : (ClientSize.Width > 200 ? ClientSize.Width : 760);
        btnCheckUpdates.Location = new Point(panelWidth - btnCheckUpdates.Width - 20, top);
        btnSettings.Location = new Point(btnCheckUpdates.Left - btnSettings.Width - spacing, top);
    }

    private void BuildServiceCards()
    {
        cardContainer.Controls.Clear();

        // Get available PHP versions
        var phpVersions = new List<string>();
        string phpRoot = Path.Combine(_appRoot, "bin", "php");
        if (Directory.Exists(phpRoot))
        {
            phpVersions.AddRange(Directory.GetDirectories(phpRoot).Select(Path.GetFileName).Where(s => !string.IsNullOrEmpty(s))!);
        }
        if (phpVersions.Count == 0)
        {
            phpVersions.Add(_config.ActivePhp);
        }

        // Add enabled service cards in reverse dock order (DockStyle.Top docks bottom-up)
        // Bottom to top: Redis -> PostgreSQL -> Mailpit -> MySQL -> PHP -> Nginx
        if (_config.EnableRedis)
        {
            var redisCard = new ServiceCard(_redis, IconKind.Server);
            cardContainer.Controls.Add(redisCard);
        }

        if (_config.EnablePostgresql)
        {
            var pgCard = new ServiceCard(_postgresql, IconKind.Database);
            cardContainer.Controls.Add(pgCard);
        }

        if (_config.EnableMailpit)
        {
            var mailpitCard = new ServiceCard(_mailpit, IconKind.Mail);
            cardContainer.Controls.Add(mailpitCard);
        }

        if (_config.EnableMysql)
        {
            var mysqlCard = new ServiceCard(_mysql, IconKind.Database);
            cardContainer.Controls.Add(mysqlCard);
        }

        if (_config.EnablePhp)
        {
            var phpCard = new ServiceCard(
                _php,
                IconKind.Lightning,
                phpVersions.ToArray(),
                _config.ActivePhp,
                async newVersion =>
                {
                    _config.ActivePhp = newVersion;
                    ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                    if (_php.Status == ServiceStatus.Running)
                    {
                        lblStatusText.Text = $"Switching PHP to {newVersion}...";
                        await _php.RestartAsync();
                        lblStatusText.Text = $"Switched to PHP {newVersion}.";
                    }
                    BuildTrayMenu();
                }
            );
            cardContainer.Controls.Add(phpCard);
        }

        if (_config.EnableNginx)
        {
            var nginxCard = new ServiceCard(_nginx, IconKind.Server);
            cardContainer.Controls.Add(nginxCard);
        }

        if (cardContainer.Controls.Count == 0)
        {
            var pnlEmpty = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            var lblEmptyTitle = new Label
            {
                Text = "No services are currently enabled",
                ForeColor = ModernColors.TextPrimary,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 32
            };

            var lblEmptySub = new Label
            {
                Text = "Activate Nginx, PHP, MySQL, Mailpit, PostgreSQL, or Redis in Settings to display them here.",
                ForeColor = ModernColors.TextSecondary,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 24
            };

            var btnOpenSettingsFromEmpty = new ModernButton
            {
                Text = "Open Settings",
                IconKind = IconKind.Gear,
                IconSize = 11,
                Width = 130,
                Height = 34,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.Primary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnOpenSettingsFromEmpty.Click += BtnSettings_Click;

            var pnlEmptyWrap = new Panel
            {
                Width = 480,
                Height = 120,
                BackColor = ModernColors.Surface,
                Padding = new Padding(20)
            };
            pnlEmptyWrap.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new RectangleF(0.5f, 0.5f, pnlEmptyWrap.Width - 1f, pnlEmptyWrap.Height - 1f);
                using var borderPen = new Pen(ModernColors.BorderSubtle, 1f);
                g.DrawRectangle(borderPen, 0, 0, pnlEmptyWrap.Width - 1, pnlEmptyWrap.Height - 1);
            };

            pnlEmpty.Resize += (s, e) =>
            {
                pnlEmptyWrap.Location = new Point((pnlEmpty.ClientSize.Width - pnlEmptyWrap.Width) / 2, Math.Max(40, (pnlEmpty.ClientSize.Height - pnlEmptyWrap.Height) / 3));
                btnOpenSettingsFromEmpty.Location = new Point((pnlEmptyWrap.Width - btnOpenSettingsFromEmpty.Width) / 2, 68);
            };

            pnlEmptyWrap.Controls.Add(btnOpenSettingsFromEmpty);
            pnlEmptyWrap.Controls.Add(lblEmptySub);
            pnlEmptyWrap.Controls.Add(lblEmptyTitle);
            pnlEmpty.Controls.Add(pnlEmptyWrap);
            cardContainer.Controls.Add(pnlEmpty);
        }

        LayoutActionButtons();
        UpdateServiceStats();
    }

    private void BuildTrayMenu()
    {
        trayMenu.Items.Clear();

        trayMenu.Items.Add("Open Dev Lite Server", null, (s, e) => RestoreFromTray());
        trayMenu.Items.Add(new ToolStripSeparator());

        trayMenu.Items.Add("Start All Services", null, async (s, e) => await StartAllServicesAsync());
        trayMenu.Items.Add("Stop All Services", null, async (s, e) => await StopAllServicesAsync());
        trayMenu.Items.Add(new ToolStripSeparator());

        // PHP Switcher submenu
        var phpSubMenu = new ToolStripMenuItem("PHP Version");
        string phpRoot = Path.Combine(_appRoot, "bin", "php");
        if (Directory.Exists(phpRoot))
        {
            foreach (var dir in Directory.GetDirectories(phpRoot))
            {
                string dirName = Path.GetFileName(dir);
                var item = new ToolStripMenuItem(dirName, null, async (s, e) =>
                {
                    _config.ActivePhp = dirName;
                    ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                    if (_php.Status == ServiceStatus.Running)
                    {
                        await _php.RestartAsync();
                    }
                    BuildServiceCards();
                    BuildTrayMenu();
                })
                {
                    Checked = dirName.Equals(_config.ActivePhp, StringComparison.OrdinalIgnoreCase)
                };
                phpSubMenu.DropDownItems.Add(item);
            }
        }
        trayMenu.Items.Add(phpSubMenu);

        // Auto-start submenu
        var autoStartMenu = new ToolStripMenuItem("Auto-start Services");
        var masterAutoStartItem = new ToolStripMenuItem("Enable Auto-start", null, (s, e) =>
        {
            _config.AutoStartServices = !_config.AutoStartServices;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            if (s is ToolStripMenuItem mi) mi.Checked = _config.AutoStartServices;
        })
        {
            Checked = _config.AutoStartServices
        };
        autoStartMenu.DropDownItems.Add(masterAutoStartItem);
        autoStartMenu.DropDownItems.Add(new ToolStripSeparator());

        void AddServiceAutoStartItem(string label, Func<bool> getter, Action<bool> setter)
        {
            var item = new ToolStripMenuItem(label, null, (s, e) =>
            {
                bool newVal = !getter();
                setter(newVal);
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                if (s is ToolStripMenuItem mi) mi.Checked = newVal;
            })
            {
                Checked = getter()
            };
            autoStartMenu.DropDownItems.Add(item);
        }

        AddServiceAutoStartItem("Nginx", () => _config.AutoStartNginx, v => _config.AutoStartNginx = v);
        AddServiceAutoStartItem("PHP FastCGI", () => _config.AutoStartPhp, v => _config.AutoStartPhp = v);
        AddServiceAutoStartItem("MySQL", () => _config.AutoStartMysql, v => _config.AutoStartMysql = v);
        AddServiceAutoStartItem("Mailpit", () => _config.AutoStartMailpit, v => _config.AutoStartMailpit = v);
        AddServiceAutoStartItem("PostgreSQL", () => _config.AutoStartPostgresql, v => _config.AutoStartPostgresql = v);
        AddServiceAutoStartItem("Redis", () => _config.AutoStartRedis, v => _config.AutoStartRedis = v);

        trayMenu.Items.Add(autoStartMenu);

        // Enabled services submenu
        var enabledServicesMenu = new ToolStripMenuItem("Enabled Services");
        void AddServiceEnabledMenuItem(string label, string serviceKey, Func<bool> getter, Action<bool> setter)
        {
            var item = new ToolStripMenuItem(label, null, (s, e) =>
            {
                bool newVal = !getter();
                setter(newVal);
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                if (s is ToolStripMenuItem mi) mi.Checked = newVal;
                BuildServiceCards();
                lblStatusText.Text = newVal
                    ? $"✔ {label} enabled (will start on Start All / Auto-start)."
                    : $"ℹ {label} disabled (skipped by Start All / Auto-start).";
                lblStatusText.ForeColor = newVal ? ModernColors.Success : ModernColors.TextSecondary;
            })
            {
                Checked = getter()
            };
            enabledServicesMenu.DropDownItems.Add(item);
        }

        AddServiceEnabledMenuItem("Nginx", "nginx", () => _config.EnableNginx, v => _config.EnableNginx = v);
        AddServiceEnabledMenuItem("PHP FastCGI", "php", () => _config.EnablePhp, v => _config.EnablePhp = v);
        AddServiceEnabledMenuItem("MySQL", "mysql", () => _config.EnableMysql, v => _config.EnableMysql = v);
        AddServiceEnabledMenuItem("Mailpit", "mailpit", () => _config.EnableMailpit, v => _config.EnableMailpit = v);
        AddServiceEnabledMenuItem("PostgreSQL", "postgresql", () => _config.EnablePostgresql, v => _config.EnablePostgresql = v);
        AddServiceEnabledMenuItem("Redis", "redis", () => _config.EnableRedis, v => _config.EnableRedis = v);

        trayMenu.Items.Add(enabledServicesMenu);

        var startWithWindowsItem = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
        {
            bool newVal = !_config.StartWithWindows;
            _config.StartWithWindows = newVal;
            WindowsStartup.SetStartup(newVal, Application.ExecutablePath);
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            if (s is ToolStripMenuItem mi) mi.Checked = newVal;
        })
        {
            Checked = _config.StartWithWindows
        };
        trayMenu.Items.Add(startWithWindowsItem);

        trayMenu.Items.Add("Open /www", null, (s, e) => BtnOpenWww_Click(s, e));
        trayMenu.Items.Add("Open Terminal", null, (s, e) => BtnOpenTerminal_Click(s, e));
        trayMenu.Items.Add(new ToolStripSeparator());

        trayMenu.Items.Add("Settings...", null, (s, e) => BtnSettings_Click(s, e));
        trayMenu.Items.Add("Check for Updates...", null, async (s, e) => await CheckForUpdatesAsync(manual: true));
        trayMenu.Items.Add(new ToolStripSeparator());

        trayMenu.Items.Add("Exit", null, async (s, e) => await ExitApplicationAsync());

        notifyIcon.ContextMenuStrip = trayMenu;
    }

    private async Task AutoStartConfiguredServicesAsync()
    {
        btnStartAll.Enabled = false;
        lblStatusText.Text = "Auto-starting configured services...";
        lblStatusText.ForeColor = ModernColors.TextSecondary;

        int startedCount = 0;
        foreach (var svc in _services)
        {
            if (_config.IsServiceEnabled(svc.Name) && _config.ShouldAutoStart(svc.Name) && svc.Status != ServiceStatus.Running)
            {
                await svc.StartAsync();
                startedCount++;
            }
        }

        btnStartAll.Enabled = true;
        lblStatusText.Text = startedCount > 0
            ? $"Auto-start complete ({startedCount} enabled service(s) running)."
            : "Ready - Auto-start finished (no enabled services scheduled).";
    }

    private async Task StartAllServicesAsync()
    {
        btnStartAll.Enabled = false;
        lblStatusText.Text = "Starting enabled services...";
        lblStatusText.ForeColor = ModernColors.TextSecondary;

        int startedCount = 0;
        int skippedCount = 0;
        foreach (var svc in _services)
        {
            if (!_config.IsServiceEnabled(svc.Name))
            {
                skippedCount++;
                continue;
            }

            if (svc.Status != ServiceStatus.Running)
            {
                await svc.StartAsync();
                startedCount++;
            }
        }

        btnStartAll.Enabled = true;
        lblStatusText.Text = startedCount > 0
            ? $"All enabled services processed ({startedCount} started{(skippedCount > 0 ? $", {skippedCount} disabled skipped" : "")})."
            : $"Ready{(skippedCount > 0 ? $" ({skippedCount} disabled service(s) skipped)" : "")}.";
        UpdateServiceStats();
    }

    private async Task StopAllServicesAsync()
    {
        btnStopAll.Enabled = false;
        lblStatusText.Text = "Stopping all services...";
        lblStatusText.ForeColor = ModernColors.TextSecondary;

        foreach (var svc in _services)
        {
            if (svc.Status == ServiceStatus.Running)
            {
                await svc.StopAsync();
            }
        }

        btnStopAll.Enabled = true;
        lblStatusText.Text = "All services stopped.";
        UpdateServiceStats();
    }

    private async void BtnStartAll_Click(object? sender, EventArgs e) => await StartAllServicesAsync();
    private async void BtnStopAll_Click(object? sender, EventArgs e) => await StopAllServicesAsync();
    private async void BtnExit_Click(object? sender, EventArgs e) => await ExitApplicationAsync();

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(_config, _appRoot);
        if (settingsForm.ShowDialog(this) == DialogResult.OK)
        {
            BuildServiceCards();
            BuildTrayMenu();
            UpdateAdminerState();
            lblStatusText.Text = "Settings applied successfully.";
            lblStatusText.ForeColor = ModernColors.Success;
        }
    }

    private async Task ExitApplicationAsync()
    {
        _isExitingExplicitly = true;
        btnExit.Enabled = false;
        btnStartAll.Enabled = false;
        btnStopAll.Enabled = false;
        lblStatusText.Text = "Stopping all services and exiting...";
        lblStatusText.ForeColor = ModernColors.Warning;

        await StopAllServicesAsync();
        _job.Dispose();
        notifyIcon.Visible = false;
        Application.Exit();
    }

    private void BtnOpenWww_Click(object? sender, EventArgs e)
    {
        string wwwDir = Path.Combine(_appRoot, "www");
        if (!Directory.Exists(wwwDir)) Directory.CreateDirectory(wwwDir);
        Process.Start(new ProcessStartInfo("explorer.exe", wwwDir) { UseShellExecute = true });
    }

    private void BtnOpenTerminal_Click(object? sender, EventArgs e)
    {
        TerminalLauncher.OpenTerminal(_appRoot, _config);
    }

    private void BtnOpenMailpit_Click(object? sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo($"http://localhost:{_config.MailpitWebPort}") { UseShellExecute = true });
    }

    private void BtnOpenAdminer_Click(object? sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo($"http://localhost:{_config.HttpPort}/adminer") { UseShellExecute = true });
    }

    private async void BtnCheckUpdates_Click(object? sender, EventArgs e)
    {
        await CheckForUpdatesAsync(manual: true);
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (manual)
        {
            lblStatusText.Text = "Checking for updates...";
            lblStatusText.ForeColor = ModernColors.TextSecondary;
            Cursor = Cursors.WaitCursor;
        }

        try
        {
            var updateInfo = await UpdateChecker.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                if (manual)
                {
                    lblStatusText.Text = "Could not reach update server. Check internet connection.";
                    lblStatusText.ForeColor = ModernColors.Warning;
                    MessageBox.Show(
                        this,
                        "Could not check for updates.\nPlease verify your internet connection or check the GitHub releases page manually.",
                        "Check for Updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                return;
            }

            if (manual || updateInfo.IsUpdateAvailable)
            {
                if (InvokeRequired)
                {
                    Invoke(() => ShowUpdateDialog(updateInfo));
                }
                else
                {
                    ShowUpdateDialog(updateInfo);
                }
            }
            else if (manual)
            {
                lblStatusText.Text = $"Up to date (v{updateInfo.CurrentVersion}).";
                lblStatusText.ForeColor = ModernColors.Success;
            }
        }
        catch (Exception ex)
        {
            if (manual)
            {
                lblStatusText.Text = $"Update check failed: {ex.Message}";
                lblStatusText.ForeColor = ModernColors.Danger;
            }
        }
        finally
        {
            if (manual)
            {
                Cursor = Cursors.Default;
            }
        }
    }

    private void ShowUpdateDialog(UpdateInfo updateInfo)
    {
        using var dialog = new UpdateDialog(updateInfo, async () =>
        {
            _isExitingExplicitly = true;
            await StopAllServicesAsync();
            _job.Dispose();
            notifyIcon.Visible = false;
        });
        dialog.ShowDialog(this);
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e) => RestoreFromTray();

    public void RestoreFromTray()
    {
        _hasBeenShown = true;
        if (!Visible)
        {
            Show();
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        SingleInstance.ForceForeground(Handle);
        BringToFront();
        Activate();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (_startMinimized && !_hasBeenShown)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == SingleInstance.WmShowFirstInstance)
        {
            RestoreFromTray();
        }
        base.WndProc(ref m);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isExitingExplicitly && _config.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            notifyIcon.ShowBalloonTip(1500, "Dev Lite Server", "Dev Lite Server is running in system tray.", ToolTipIcon.Info);
        }
    }
}
