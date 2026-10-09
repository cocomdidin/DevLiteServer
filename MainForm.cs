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

        _services.AddRange([_php, _nginx, _mysql, _mailpit]);

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

        // Add cards in reverse order because Dock = DockStyle.Top docks from bottom up in addition
        var mailpitCard = new ServiceCard(_mailpit, IconKind.Mail);
        var mysqlCard = new ServiceCard(_mysql, IconKind.Database);
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
        var nginxCard = new ServiceCard(_nginx, IconKind.Server);

        // Adding top to bottom
        cardContainer.Controls.Add(mailpitCard);
        cardContainer.Controls.Add(mysqlCard);
        cardContainer.Controls.Add(phpCard);
        cardContainer.Controls.Add(nginxCard);
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

        trayMenu.Items.Add(autoStartMenu);

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
            if (_config.ShouldAutoStart(svc.Name) && svc.Status != ServiceStatus.Running)
            {
                await svc.StartAsync();
                startedCount++;
            }
        }

        btnStartAll.Enabled = true;
        lblStatusText.Text = startedCount > 0
            ? $"Auto-start complete ({startedCount} service(s) running)."
            : "Ready - Auto-start finished (no services selected).";
    }

    private async Task StartAllServicesAsync()
    {
        btnStartAll.Enabled = false;
        lblStatusText.Text = "Starting all services...";
        lblStatusText.ForeColor = ModernColors.TextSecondary;

        foreach (var svc in _services)
        {
            if (svc.Status != ServiceStatus.Running)
            {
                await svc.StartAsync();
            }
        }

        btnStartAll.Enabled = true;
        lblStatusText.Text = "All services processed.";
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
    }

    private async void BtnStartAll_Click(object? sender, EventArgs e) => await StartAllServicesAsync();
    private async void BtnStopAll_Click(object? sender, EventArgs e) => await StopAllServicesAsync();
    private async void BtnExit_Click(object? sender, EventArgs e) => await ExitApplicationAsync();

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
