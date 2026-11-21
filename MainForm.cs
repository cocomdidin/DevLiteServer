using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using DevLiteServer.Core;
using DevLiteServer.Core.Downloader;
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
    private readonly RustFsService _rustfs;
    private readonly List<IService> _services = new();

    private readonly bool _startMinimized;
    private bool _allowVisible;
    private bool _hasAutoStartedServices = false;
    private bool _hasRunStartupTasks = false;
    private bool _isExitingExplicitly = false;
    private volatile bool _isShuttingDown = false;
    private int _shutdownCleanupDone = 0;

    private const int WM_QUERYENDSESSION = 0x0011;
    private const int WM_ENDSESSION = 0x0016;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ShutdownBlockReasonCreate(IntPtr hWnd, string pwszReason);

    [DllImport("user32.dll")]
    private static extern bool ShutdownBlockReasonDestroy(IntPtr hWnd);

    // Sites Page dynamic container
    private FlowLayoutPanel _pnlSitesContainer = null!;
    private FlowLayoutPanel _pnlWorkersContainer = null!;
    private readonly Dictionary<string, WorkerLogsDialog> _openWorkerLogs = new(StringComparer.OrdinalIgnoreCase);
    private Panel _pnlPhpPackages = null!;
    private Panel _pnlNodePackages = null!;
    private ModernComboBox _cmbPhpVersions = null!;
    private Label _lblPhpTitle = null!;
    private Label _lblPhpSub = null!;
    private StatusPill _pillPhp = null!;
    private Label _lblNodeTitle = null!;
    private Action? _refreshSqlServerCard;
    private Action? _refreshNodePackages;
    private Action? _refreshPhpPackages;
    private Color _sidebarDotColor = ModernColors.TextMuted;
    private readonly Dictionary<string, TunnelDialog> _openTunnelDialogs = new(StringComparer.OrdinalIgnoreCase);

    public MainForm(bool startMinimized = false)
    {
        _startMinimized = startMinimized;
        _allowVisible = !startMinimized;
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
        WorkerManager.Load(_appRoot);
        SchedulerManager.Load(_appRoot);

        _php = new PhpService(_appRoot, _job, _config);
        PhpService.UpdateCurrentJunction(_appRoot, _config.ActivePhp);
        NodeManager.UpdateCurrentJunction(_appRoot, _config.ActiveNode);
        // Automatically sync LiteServer runtimes (PHP, Node, Composer) to Windows User PATH (Zero-UAC)
        UserEnvironmentManager.SetRegistration(_appRoot, true);
        _nginx = new NginxService(_appRoot, _job, _config, _php);
        _mysql = new MySqlService(_appRoot, _job, _config);
        _mailpit = new MailpitService(_appRoot, _job, _config);
        _postgresql = new PostgreSqlService(_appRoot, _job, _config);
        _redis = new RedisService(_appRoot, _job, _config);
        _rustfs = new RustFsService(_appRoot, _job, _config);

        _services.AddRange([_php, _nginx, _mysql, _mailpit, _postgresql, _redis, _rustfs]);

        foreach (var svc in _services)
        {
            svc.PortConflictResolver = PromptPortConflictResolverAsync;
            svc.StatusChanged += (s, status) =>
            {
                if (_isShuttingDown || IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired)
                {
                    try { BeginInvoke(() => { if (!_isShuttingDown && !IsDisposed) OnServiceStatusChanged(s, status); }); } catch { }
                }
                else
                {
                    OnServiceStatusChanged(s, status);
                }
            };
        }

        InitPages();
        BuildServiceCards();
        BuildTrayMenu();
        UpdateServiceStats();
        SelectNavTab(0);

        if (_config.EnableLocalDns)
        {
            DnsServer.Instance.Start();
        }

        InitFooterErrorCopy();

        // Check for Visual C++ runtime
        if (!DependencyChecker.IsVcRedistInstalled())
        {
            lblStatusText.Text = "⚠ Warning: Visual C++ Redistributable (x64) is missing. Click here to install.";
            lblStatusText.ForeColor = ModernColors.Warning;
            lblStatusText.Cursor = Cursors.Hand;
        }
        else
        {
            lblStatusText.Text = $"Ready | Root: {_appRoot}";
            lblStatusText.ForeColor = ModernColors.TextSecondary;
            lblStatusText.Cursor = Cursors.Default;
        }

        lblStatusText.Click += (s, e) =>
        {
            if (!DependencyChecker.IsVcRedistInstalled())
            {
                using var dlg = new VcRedistDialog();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    lblStatusText.Text = $"Ready | Root: {_appRoot}";
                    lblStatusText.ForeColor = ModernColors.TextSecondary;
                    lblStatusText.Cursor = Cursors.Default;
                }
            }
        };

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

        Shown += (s, e) =>
        {
            // First-run auto-setup: provision core environment (MSVCRT, Nginx, PHP 8.4, Composer, Git, Adminer) if missing
            if (CoreProvisioner.NeedsSetup(_appRoot))
            {
                using var setupDlg = new FirstRunSetupDialog(_appRoot, _config);
                if (setupDlg.ShowDialog(this) == DialogResult.OK)
                {
                    _nginx.CheckInstallation();
                    _php.CheckInstallation();
                    BuildServiceCards();
                    RefreshPhpVersionsCombo();
                    RefreshSitesList();
                    _refreshPhpPackages?.Invoke();
                    BuildTrayMenu();
                    lblStatusText.Text = "Core environment provisioned successfully.";
                    lblStatusText.ForeColor = ModernColors.Success;
                }
            }
        };
    }

    // ==========================================
    // NAVIGATION & TAB SWITCHING
    // ==========================================
    private void SelectNavTab(int index)
    {
        navDashboard.IsActive = (index == 0);
        navSites.IsActive = (index == 1);
        navPhp.IsActive = (index == 2);
        navNode.IsActive = (index == 3);
        navDatabases.IsActive = (index == 4);
        navServices.IsActive = (index == 5);
        navWorkers.IsActive = (index == 6);
        navSettings.IsActive = (index == 7);

        pageDashboard.Visible = (index == 0);
        pageSites.Visible = (index == 1);
        pagePhp.Visible = (index == 2);
        pageNode.Visible = (index == 3);
        pageServices.Visible = (index == 4);
        pageDevServices.Visible = (index == 5);
        pageWorkers.Visible = (index == 6);
        pageSettings.Visible = (index == 7);

        switch (index)
        {
            case 0:
                lblPageTitle.Text = "Dashboard";
                lblPageSubtitle.Text = "Environment overview and service controller";
                break;
            case 1:
                lblPageTitle.Text = "Sites & Local Domains";
                lblPageSubtitle.Text = "Virtual hosts and project directories in /www";
                RefreshSitesList();
                break;
            case 2:
                lblPageTitle.Text = "PHP FastCGI";
                lblPageSubtitle.Text = "Multi-version switch, worker pool, and extensions";
                break;
            case 3:
                lblPageTitle.Text = "Node.js Runtime";
                lblPageSubtitle.Text = "Portable and Windows system Node.js version management";
                break;
            case 4:
                lblPageTitle.Text = "Databases & Cache";
                lblPageSubtitle.Text = "Manage MySQL, PostgreSQL, Redis, and GUI client tools";
                break;
            case 5:
                lblPageTitle.Text = "Services";
                lblPageSubtitle.Text = "Local Mailpit SMTP/Webmail and RustFS S3 object storage mock";
                break;
            case 6:
                lblPageTitle.Text = "Background Workers";
                lblPageSubtitle.Text = "Windows Supervisor for queues, dev servers, and scheduled tasks";
                RefreshWorkersList();
                break;
            case 7:
                lblPageTitle.Text = "Settings";
                lblPageSubtitle.Text = "System integration, tray behavior, and automation preferences";
                break;
        }

        LayoutTopRibbonButtons();
    }

    // ==========================================
    // INITIALIZE PAGES
    // ==========================================
    private void InitPages()
    {
        Panel[] mainPages = [pageDashboard, pageSites, pagePhp, pageNode, pageServices, pageDevServices, pageWorkers, pageSettings];
        foreach (var page in mainPages)
        {
            page.Dock = DockStyle.Fill;
            page.BackColor = ModernColors.Background;
            page.AutoScroll = true;
            page.Visible = false;
            pageContainer.Controls.Add(page);
        }

        PopulateDashboardPage();
        PopulateSitesPage();
        PopulatePhpPage();
        PopulateNodePage();
        PopulateServicesPage();
        PopulateDevServicesPage();
        PopulateWorkersPage();
        PopulateSettingsPage();
    }

    // ------------------------------------------
    // PAGE 0: DASHBOARD
    // ------------------------------------------
    private void PopulateDashboardPage()
    {
        pageDashboard.Padding = new Padding(20, 16, 20, 16);

        cardContainer.Dock = DockStyle.Fill;
        cardContainer.BackColor = ModernColors.Background;
        cardContainer.AutoScroll = true;

        pageDashboard.Controls.Add(cardContainer);
    }

    // ------------------------------------------
    // PAGE 6: SETTINGS
    // ------------------------------------------
    // PAGE: SETTINGS
    // ------------------------------------------
    private void PopulateSettingsPage()
    {
        pageSettings.Padding = new Padding(24, 16, 24, 20);

        // ==========================================
        // 1. System & Lifecycle
        // ==========================================
        var secSystem = CreateSettingsSectionHeader("System & Lifecycle", isFirst: true);

        // Start on Windows Logon
        var togWindows = new ModernToggle { Checked = _config.StartWithWindows };
        togWindows.CheckedChanged += (s, e) =>
        {
            _config.StartWithWindows = togWindows.Checked;
            WindowsStartup.SetStartup(_config.StartWithWindows, Application.ExecutablePath);
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        };
        var cardWin = CreateOptionCard("Start with Windows Logon", "Automatically launch Dev Lite Server into system tray when logging in.", togWindows);

        // Minimize to Tray
        var togTray = new ModernToggle { Checked = _config.MinimizeToTray };
        togTray.CheckedChanged += (s, e) =>
        {
            _config.MinimizeToTray = togTray.Checked;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        };
        var cardTray = CreateOptionCard("Minimize to System Tray", "Closing the main window keeps services running in the background tray.", togTray);

        // Auto-start enabled services on launch
        var togAuto = new ModernToggle { Checked = _config.AutoStartServices };
        togAuto.CheckedChanged += (s, e) =>
        {
            _config.AutoStartServices = togAuto.Checked;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            BuildTrayMenu();
        };
        var cardAuto = CreateOptionCard("Auto-start Enabled Services", "Automatically launch all enabled services when Dev Lite Server opens.", togAuto);

        // ==========================================
        // 2. Developer Environment
        // ==========================================
        var secDev = CreateSettingsSectionHeader("Developer Environment");

        // Preferred Code Editor
        var cmbEditor = new ModernComboBox
        {
            Width = 160,
            Height = 28
        };
        foreach (var ed in CodeEditorManager.SupportedEditors)
        {
            cmbEditor.Items.Add(ed.DisplayName);
        }
        int selIdx = Array.FindIndex(CodeEditorManager.SupportedEditors, e => e.Id.Equals(_config.PreferredEditor, StringComparison.OrdinalIgnoreCase));
        cmbEditor.SelectedIndex = selIdx >= 0 ? selIdx : 0;

        var btnBrowseEditor = new ModernButton
        {
            Text = "Browse...",
            Width = 72,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f),
            Visible = _config.PreferredEditor.Equals("custom", StringComparison.OrdinalIgnoreCase)
        };

        var pnlEditorCtrl = new Panel
        {
            Width = 240,
            Height = 30,
            BackColor = Color.Transparent
        };
        cmbEditor.Location = new Point(0, 0);
        btnBrowseEditor.Location = new Point(166, 0);
        pnlEditorCtrl.Controls.Add(cmbEditor);
        pnlEditorCtrl.Controls.Add(btnBrowseEditor);

        cmbEditor.SelectedIndexChanged += (s, e) =>
        {
            if (cmbEditor.SelectedIndex >= 0 && cmbEditor.SelectedIndex < CodeEditorManager.SupportedEditors.Length)
            {
                var chosen = CodeEditorManager.SupportedEditors[cmbEditor.SelectedIndex];
                _config.PreferredEditor = chosen.Id;
                btnBrowseEditor.Visible = chosen.Id.Equals("custom", StringComparison.OrdinalIgnoreCase);

                if (chosen.Id.Equals("custom", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(_config.CustomEditorPath))
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Select Code Editor Executable",
                        Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                        CheckFileExists = true
                    };
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        _config.CustomEditorPath = ofd.FileName;
                    }
                }

                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                RefreshSitesList();
            }
        };

        btnBrowseEditor.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Code Editor Executable",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                CheckFileExists = true,
                FileName = _config.CustomEditorPath
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _config.CustomEditorPath = ofd.FileName;
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                RefreshSitesList();
            }
        };

        var cardEditor = CreateOptionCard("Preferred Code Editor", "Choose the editor used to open site codebases from the Sites tab.", pnlEditorCtrl);

        // ==========================================
        // 3. Maintenance & Configuration
        // ==========================================
        var secMaint = CreateSettingsSectionHeader("Maintenance & Configuration");

        var pnlActions = new Panel
        {
            Height = 44,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnOpenConfig = new ModernButton
        {
            Text = "Open config.ini",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 130,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(0, 6)
        };
        btnOpenConfig.Click += (s, e) =>
        {
            string cfgFile = Path.Combine(_appRoot, "config.ini");
            if (File.Exists(cfgFile))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{cfgFile}\"") { UseShellExecute = true });
            }
        };

        var btnResetDefaults = new ModernButton
        {
            Text = "Reset to Defaults",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 140,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(140, 6)
        };
        btnResetDefaults.Click += (s, e) =>
        {
            var res = MessageBox.Show(this, "Are you sure you want to reset all preferences to default values?", "Reset Preferences", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                _config.ResetToDefaults();
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                BuildServiceCards();
                BuildTrayMenu();
                lblStatusText.Text = "Preferences reset to default values.";
                lblStatusText.ForeColor = ModernColors.Success;
            }
        };

        var btnCoreSetup = new ModernButton
        {
            Text = "Core Setup Wizard",
            IconKind = IconKind.Lightning,
            IconSize = 10,
            Width = 145,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(290, 6)
        };
        btnCoreSetup.Click += (s, e) =>
        {
            using var dlg = new FirstRunSetupDialog(_appRoot, _config);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _nginx.CheckInstallation();
                _php.CheckInstallation();
                BuildServiceCards();
                RefreshPhpVersionsCombo();
                RefreshSitesList();
                _refreshPhpPackages?.Invoke();
                BuildTrayMenu();
            }
        };

        pnlActions.Controls.Add(btnOpenConfig);
        pnlActions.Controls.Add(btnResetDefaults);
        pnlActions.Controls.Add(btnCoreSetup);

        // Add to settings page in forward logical order
        Control[] settingsControls = [
            secSystem,
            cardWin,
            cardTray,
            cardAuto,
            secDev,
            cardEditor,
            secMaint,
            pnlActions
        ];

        pageSettings.Controls.AddRange(settingsControls.Reverse().ToArray());
    }

    // ------------------------------------------
    // PAGE 2: SITES
    // ------------------------------------------
    private void PopulateSitesPage()
    {
        pageSites.Padding = new Padding(24, 16, 24, 20);

        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(0, 0, 0, 8)
        };

        var btnAddSite = new ModernButton
        {
            Text = "Add Site",
            IconKind = IconKind.Plus,
            IconSize = 10,
            Width = 92,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(0, 2)
        };
        btnAddSite.Click += async (s, e) =>
        {
            using var dlg = new SiteEditDialog(_appRoot, _config);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultSite != null)
            {
                VirtualHostManager.GenerateVhostsConfig(_appRoot, _config, Path.Combine(_nginx.GetNginxDirectory(), "conf"));
                _ = _php.EnsureWorkersForConfiguredSitesAsync();
                if (_nginx.Status == ServiceStatus.Running)
                {
                    await _nginx.RestartAsync();
                }
                RefreshSitesList();
                lblStatusText.Text = $"Site '{dlg.ResultSite.Domain}' configured.";
                lblStatusText.ForeColor = ModernColors.Success;
            }
        };

        var btnSyncDomains = new ModernButton
        {
            Text = "Sync Domains",
            IconKind = IconKind.Lightning,
            IconSize = 10,
            Width = 118,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(104, 2)
        };

        var btnRefresh = new ModernButton
        {
            Text = "Refresh",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 84,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(230, 2)
        };
        btnRefresh.Click += (s, e) => RefreshSitesList();

        var btnOpenFolder = new ModernButton
        {
            Text = "Open /www",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 105,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(322, 2)
        };
        btnOpenFolder.Click += BtnOpenWww_Click;

        var btnOpenConf = new ModernButton
        {
            Text = "nginx.conf",
            IconKind = IconKind.Gear,
            IconSize = 10,
            Width = 95,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(435, 2)
        };
        btnOpenConf.Click += (s, e) =>
        {
            string confFile = Path.Combine(_nginx.GetNginxDirectory(), "conf", "nginx.conf");
            if (File.Exists(confFile))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{confFile}\"") { UseShellExecute = true });
            }
        };

        void UpdateDomainSyncButtonState()
        {
            if (_isShuttingDown || IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(UpdateDomainSyncButtonState); } catch { } return; }
            if (DnsServer.IsZeroUacActive)
            {
                btnSyncDomains.Text = "Domains Active";
                btnSyncDomains.IconKind = IconKind.Check;
                btnSyncDomains.NormalColor = ModernColors.SuccessBg;
                btnSyncDomains.HoverColor = ModernColors.SuccessBg;
                btnSyncDomains.ForeColor = ModernColors.Success;
                btnSyncDomains.ShowBorder = true;
                btnSyncDomains.BorderLineColor = Color.FromArgb(120, ModernColors.Success);
                btnSyncDomains.Width = 125;
            }
            else
            {
                btnSyncDomains.Text = "Sync Domains";
                btnSyncDomains.IconKind = IconKind.Lightning;
                btnSyncDomains.NormalColor = ModernColors.Primary;
                btnSyncDomains.HoverColor = ModernColors.PrimaryHover;
                btnSyncDomains.ForeColor = Color.White;
                btnSyncDomains.ShowBorder = false;
                btnSyncDomains.BorderLineColor = Color.Transparent;
                btnSyncDomains.Width = 118;
            }

            btnRefresh.Location = new Point(btnSyncDomains.Right + 8, 2);
            btnOpenFolder.Location = new Point(btnRefresh.Right + 8, 2);
            btnOpenConf.Location = new Point(btnOpenFolder.Right + 8, 2);
            btnSyncDomains.Invalidate();
        }

        UpdateDomainSyncButtonState();
        DnsServer.Instance.StatusChanged += _ => UpdateDomainSyncButtonState();

        btnSyncDomains.Click += async (s, e) =>
        {
            btnSyncDomains.Enabled = false;

            try
            {
                // Step 1: Check if Zero-UAC local DNS is already fully active
                if (DnsServer.IsZeroUacActive)
                {
                    lblStatusText.Text = "Local DNS active (Zero-UAC). All *.test domains resolve dynamically.";
                    lblStatusText.ForeColor = ModernColors.Success;
                    UpdateDomainSyncButtonState();
                    return;
                }

                // Step 2: Ensure local DNS server is started if enabled in config
                if (!DnsServer.Instance.IsRunning)
                {
                    if (DnsServer.Instance.Start())
                    {
                        _config.EnableLocalDns = true;
                        ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                    }
                }

                // Step 3: If DNS server is running, offer 1-click Windows DNS configuration (once)
                if (DnsServer.Instance.IsRunning && !DnsServer.IsWindowsDnsConfigured())
                {
                    var askDns = MessageBox.Show(this,
                        "Enable Zero-UAC DNS mode?\n\nThis configures your network adapter to route *.test domains automatically via DevLite's Micro DNS server (UAC prompt once, no hosts file edits needed ever again).\n\nClick 'Yes' to enable Zero-UAC DNS, or 'No' to fallback to Windows hosts file sync.",
                        "Domain Resolution Setup",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (askDns == DialogResult.Yes)
                    {
                        lblStatusText.Text = "Configuring Windows DNS for Zero-UAC mode...";
                        lblStatusText.ForeColor = ModernColors.Primary;
                        bool configured = await DnsServer.ConfigureWindowsDnsAsync(true);
                        if (configured && DnsServer.IsWindowsDnsConfigured())
                        {
                            lblStatusText.Text = "Zero-UAC DNS enabled! All *.test domains resolve instantly without hosts sync.";
                            lblStatusText.ForeColor = ModernColors.Success;
                            UpdateDomainSyncButtonState();
                            return;
                        }
                        else
                        {
                            lblStatusText.Text = "DNS adapter setup bypassed or failed. Falling back to hosts file sync...";
                            lblStatusText.ForeColor = ModernColors.Warning;
                        }
                    }
                }

                // Step 4: Fallback to hosts file sync
                lblStatusText.Text = "Syncing *.test virtual domains to Windows hosts file (fallback)...";
                lblStatusText.ForeColor = ModernColors.Primary;
                bool ok = await VirtualHostManager.SyncHostsFileBatchAsync(_appRoot);
                if (ok)
                {
                    lblStatusText.Text = "Windows hosts file synced with all *.test domains.";
                    lblStatusText.ForeColor = ModernColors.Success;
                    if (_nginx.Status == ServiceStatus.Running)
                    {
                        await _nginx.RestartAsync();
                    }
                }
                else
                {
                    lblStatusText.Text = "Hosts sync canceled or failed.";
                    lblStatusText.ForeColor = ModernColors.Warning;
                }
            }
            finally
            {
                btnSyncDomains.Enabled = true;
                UpdateDomainSyncButtonState();
            }
        };

        // Row 2: VHost Ports configuration bar
        var lblPortsTag = new Label
        {
            Text = "VHost Ports:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 44)
        };

        var lblHttp = new Label
        {
            Text = "HTTP:",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(78, 44)
        };
        var txtHttp = CreatePortInput(_config.HttpPort, p =>
        {
            _config.HttpPort = p;
            _nginx.UpdatePort(p);
            RefreshSitesList();
        });
        txtHttp.Location = new Point(118, 40);

        var lblHttps = new Label
        {
            Text = "HTTPS:",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(188, 44)
        };
        var txtHttps = CreatePortInput(_config.HttpsPort, p =>
        {
            _config.HttpsPort = p;
            VirtualHostManager.GenerateVhostsConfig(_appRoot, _config, Path.Combine(_nginx.GetNginxDirectory(), "conf"));
            RefreshSitesList();
        });
        txtHttps.Location = new Point(236, 40);

        var lblPortsHint = new Label
        {
            Text = "•  (auto-configures virtual host listeners and TLS certificates)",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
            AutoSize = true,
            Location = new Point(txtHttps.Right + 12, 45)
        };

        void LayoutSitesTop()
        {
            // Row 1 buttons:
            btnAddSite.Location = new Point(0, 2);
            btnSyncDomains.Location = new Point(btnAddSite.Right + 8, 2);
            btnRefresh.Location = new Point(btnSyncDomains.Right + 8, 2);
            btnOpenFolder.Location = new Point(btnRefresh.Right + 8, 2);
            btnOpenConf.Location = new Point(btnOpenFolder.Right + 8, 2);

            // Row 2 ports:
            lblPortsTag.Location = new Point(0, 44);
            lblHttp.Location = new Point(lblPortsTag.Right + 10, 44);
            txtHttp.Location = new Point(lblHttp.Right + 5, 40);
            lblHttps.Location = new Point(txtHttp.Right + 14, 44);
            txtHttps.Location = new Point(lblHttps.Right + 5, 40);
            lblPortsHint.Location = new Point(txtHttps.Right + 12, 45);

            int maxHintW = pnlTop.ClientSize.Width - lblPortsHint.Left - 10;
            if (maxHintW > 40)
            {
                lblPortsHint.MaximumSize = new Size(maxHintW, 20);
                lblPortsHint.AutoEllipsis = true;
                lblPortsHint.Visible = true;
            }
            else
            {
                lblPortsHint.Visible = false;
            }
        }
        pnlTop.Resize += (s, e) => LayoutSitesTop();
        LayoutSitesTop();

        pnlTop.Controls.Add(btnAddSite);
        pnlTop.Controls.Add(btnSyncDomains);
        pnlTop.Controls.Add(btnRefresh);
        pnlTop.Controls.Add(btnOpenFolder);
        pnlTop.Controls.Add(btnOpenConf);
        pnlTop.Controls.Add(lblPortsTag);
        pnlTop.Controls.Add(lblHttp);
        pnlTop.Controls.Add(txtHttp);
        pnlTop.Controls.Add(lblHttps);
        pnlTop.Controls.Add(txtHttps);
        pnlTop.Controls.Add(lblPortsHint);

        _pnlSitesContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _pnlSitesContainer.Resize += (s, e) =>
        {
            int w = _pnlSitesContainer.ClientSize.Width > 200 ? _pnlSitesContainer.ClientSize.Width - 10 : 640;
            foreach (Control c in _pnlSitesContainer.Controls)
            {
                if (c.Width != w) c.Width = w;
            }
        };

        pageSites.Controls.Add(_pnlSitesContainer);
        pageSites.Controls.Add(pnlTop);
        pnlTop.SendToBack();
    }

    private void RefreshSitesList()
    {
        if (_pnlSitesContainer == null) return;
        _pnlSitesContainer.Controls.Clear();

        string wwwDir = Path.Combine(_appRoot, "www");
        if (!Directory.Exists(wwwDir))
        {
            Directory.CreateDirectory(wwwDir);
        }

        int cardWidth = _pnlSitesContainer.ClientSize.Width > 400 ? _pnlSitesContainer.ClientSize.Width - 10 : 620;

        // 1. Root Localhost Site
        var rootCard = CreateRootSiteCard(cardWidth, wwwDir);
        _pnlSitesContainer.Controls.Add(rootCard);

        // 2. All managed sites (both in /www and external)
        var sites = VirtualHostManager.GetAllSites(_appRoot);
        foreach (var site in sites)
        {
            var siteCard = CreateManagedSiteCard(site, cardWidth);
            _pnlSitesContainer.Controls.Add(siteCard);
        }
    }

    private Panel CreateRootSiteCard(int width, string wwwDir)
    {
        string url = $"http://localhost:{_config.HttpPort}";
        var card = new Panel
        {
            Width = width,
            Height = 72,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 6, 12, 16)
        };

        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = card.Height - 10f;
            var rect = new RectangleF(halfStroke, halfStroke, card.Width - stroke, cardH - stroke);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bgBrush = new SolidBrush(ModernColors.Card);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.Border, stroke);
            g.DrawPath(pen, path);

            // Badge
            var badgeRect = new RectangleF(14, (cardH - 34) / 2f, 34, 34);
            using var badgeBg = new SolidBrush(Color.FromArgb(45, 63, 87));
            using var badgeBorder = new Pen(ModernColors.Border, 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(22, (cardH - 34) / 2f + 8, 18, 18);
            VectorIcons.Draw(g, IconKind.Server, iconRect, Color.FromArgb(120, 150, 215));
        };

        var lblName = new Label
        {
            Text = "localhost",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(60, 10),
            AutoSize = true,
            AutoEllipsis = true,
            Cursor = Cursors.Hand
        };
        lblName.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        };

        var lblUrl = new Label
        {
            Text = $"{url}  •  Default Web Root  •  {wwwDir}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(61, 33),
            AutoSize = true,
            AutoEllipsis = true
        };

        var tip = new ToolTip();
        tip.SetToolTip(lblName, $"Open {url} in browser");
        tip.SetToolTip(lblUrl, $"Path: {wwwDir}");

        var btnBrowse = new ModernButton
        {
            Text = "Open",
            IconKind = IconKind.Globe,
            IconSize = 10,
            Width = 64,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            CustomSurfaceColor = ModernColors.Card,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnBrowse.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        };
        tip.SetToolTip(btnBrowse, $"Open {url} in browser");

        var btnOpenDir = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Folder,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = ModernColors.Card,
            ForeColor = ModernColors.TextSecondary
        };
        btnOpenDir.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", wwwDir) { UseShellExecute = true }); } catch { }
        };
        tip.SetToolTip(btnOpenDir, $"Open folder: {wwwDir}");

        string editorName = CodeEditorManager.GetEditorDisplayName(_config.PreferredEditor);
        var btnOpenCode = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Code,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = ModernColors.Card,
            ForeColor = ModernColors.TextSecondary
        };
        btnOpenCode.Click += (s, e) =>
        {
            CodeEditorManager.OpenFolder(_config, wwwDir, this);
        };
        tip.SetToolTip(btnOpenCode, $"Open codebase in {editorName}: {wwwDir}");

        void LayoutRootSiteButtons()
        {
            int cardH = card.Height - 10;
            int rightX = card.Width > 200 ? card.Width - btnBrowse.Width - 14 : 500;
            btnBrowse.Location = new Point(rightX, (cardH - btnBrowse.Height) / 2);
            btnOpenDir.Location = new Point(btnBrowse.Left - btnOpenDir.Width - 6, (cardH - btnOpenDir.Height) / 2);
            btnOpenCode.Location = new Point(btnOpenDir.Left - btnOpenCode.Width - 6, (cardH - btnOpenCode.Height) / 2);

            int maxTextW = btnOpenCode.Left - lblName.Left - 12;
            if (maxTextW > 50)
            {
                lblName.MaximumSize = new Size(maxTextW, 22);
                lblUrl.MaximumSize = new Size(maxTextW, 20);
            }
        }
        card.Resize += (s, e) => LayoutRootSiteButtons();
        LayoutRootSiteButtons();

        card.Controls.Add(lblName);
        card.Controls.Add(lblUrl);
        card.Controls.Add(btnOpenCode);
        card.Controls.Add(btnOpenDir);
        card.Controls.Add(btnBrowse);

        return card;
    }

    private Panel CreateManagedSiteCard(SiteItem site, int width)
    {
        string scheme = site.SslEnabled ? "https" : "http";
        int port = site.SslEnabled ? _config.HttpsPort : _config.HttpPort;
        string portSuffix = (site.SslEnabled && port == 443) || (!site.SslEnabled && port == 80) ? "" : $":{port}";
        string url = $"{scheme}://{site.Domain}{portSuffix}";

        var card = new Panel
        {
            Width = width,
            Height = 72,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 6, 12, 16)
        };

        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = card.Height - 10f;
            var rect = new RectangleF(halfStroke, halfStroke, card.Width - stroke, cardH - stroke);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bgBrush = new SolidBrush(site.SslEnabled ? Color.FromArgb(13, 60, 70) : ModernColors.Card);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(site.SslEnabled ? Color.FromArgb(64, 83, 100) : ModernColors.Border, stroke);
            g.DrawPath(pen, path);

            // Icon circle badge
            var badgeRect = new RectangleF(14, (cardH - 34) / 2f, 34, 34);
            using var badgeBg = new SolidBrush(site.SslEnabled ? Color.FromArgb(23, 87, 89) : Color.FromArgb(45, 63, 87));
            using var badgeBorder = new Pen(site.SslEnabled ? Color.FromArgb(40, 180, 150) : ModernColors.Border, 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(22, (cardH - 34) / 2f + 8, 18, 18);
            if (site.SslEnabled)
            {
                VectorIcons.Draw(g, IconKind.Lock, iconRect, Color.FromArgb(113, 207, 193));
            }
            else
            {
                VectorIcons.Draw(g, IconKind.Server, iconRect, Color.FromArgb(120, 150, 215));
            }
        };

        var lblName = new Label
        {
            Text = site.Domain,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(60, 10),
            AutoSize = true,
            AutoEllipsis = true,
            Cursor = Cursors.Hand
        };
        lblName.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        };

        string phpDesc = site.IsProxy
            ? $"Proxy :{site.ProxyPort} (Node/SSR)"
            : (string.IsNullOrEmpty(site.PhpVersion) || site.PhpVersion.Equals("default", StringComparison.OrdinalIgnoreCase)
                ? $"PHP {_config.ActivePhp} (Default)"
                : site.PhpVersion);

        string sslDesc = site.SslEnabled ? "SSL (443)" : "HTTP";
        string locationDesc = site.IsProxy
            ? "Reverse Proxy"
            : (site.IsExternal ? "External" : (site.HasPublicSubfolder ? "public/" : "Local"));

        var lblUrl = new Label
        {
            Text = $"{url}  •  {sslDesc}  •  {phpDesc}  •  {locationDesc}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(61, 33),
            AutoSize = true,
            AutoEllipsis = true
        };

        var tip = new ToolTip();
        tip.SetToolTip(lblName, $"Open {url} in browser\nPath: {site.PhysicalPath}\nDocRoot: {site.DocumentRoot}");
        tip.SetToolTip(lblUrl, $"Path: {site.PhysicalPath}\nDocRoot: {site.DocumentRoot}");

        Color cardBg = site.SslEnabled ? Color.FromArgb(13, 60, 70) : ModernColors.Card;

        var btnBrowse = new ModernButton
        {
            Text = "Open",
            IconKind = IconKind.Globe,
            IconSize = 10,
            Width = 64,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = false,
            NormalColor = site.SslEnabled ? ModernColors.Success : ModernColors.Primary,
            HoverColor = site.SslEnabled ? ModernColors.SuccessHover : ModernColors.PrimaryHover,
            CustomSurfaceColor = cardBg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnBrowse.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        };
        tip.SetToolTip(btnBrowse, $"Open {url} in browser");

        var btnOpenDir = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Folder,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = cardBg,
            ForeColor = ModernColors.TextSecondary
        };
        btnOpenDir.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", site.PhysicalPath) { UseShellExecute = true }); } catch { }
        };
        tip.SetToolTip(btnOpenDir, $"Open folder: {site.PhysicalPath}");

        string editorName = CodeEditorManager.GetEditorDisplayName(_config.PreferredEditor);
        var btnOpenCode = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Code,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = cardBg,
            ForeColor = ModernColors.TextSecondary
        };
        btnOpenCode.Click += (s, e) =>
        {
            CodeEditorManager.OpenFolder(_config, site.PhysicalPath, this);
        };
        tip.SetToolTip(btnOpenCode, $"Open codebase in {editorName}: {site.PhysicalPath}");

        var btnShare = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Lightning,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = cardBg,
            ForeColor = CloudflareTunnelManager.IsTunnelActive(site.Domain) ? ModernColors.Success : ModernColors.TextSecondary
        };
        btnShare.Click += (s, e) =>
        {
            if (_openTunnelDialogs.TryGetValue(site.Domain, out var existingDlg) && !existingDlg.IsDisposed)
            {
                if (existingDlg.WindowState == FormWindowState.Minimized)
                    existingDlg.WindowState = FormWindowState.Normal;
                existingDlg.BringToFront();
                existingDlg.Focus();
                return;
            }

            var dlg = new TunnelDialog(_appRoot, _job, site, _config);
            _openTunnelDialogs[site.Domain] = dlg;
            dlg.FormClosed += (_, _) =>
            {
                _openTunnelDialogs.Remove(site.Domain);
                if (!IsDisposed)
                {
                    Invoke(() =>
                    {
                        btnShare.ForeColor = CloudflareTunnelManager.IsTunnelActive(site.Domain) ? ModernColors.Success : ModernColors.TextSecondary;
                    });
                }
            };
            dlg.Show(this);
            btnShare.ForeColor = CloudflareTunnelManager.IsTunnelActive(site.Domain) ? ModernColors.Success : ModernColors.TextSecondary;
        };
        tip.SetToolTip(btnShare, $"Share {site.Domain} via Cloudflare Tunnel (One-click public URL)");

        var linkedWorker = WorkerManager.FindByDomain(site.Domain);
        var btnWorker = new ModernButton
        {
            Text = "",
            IconKind = linkedWorker != null && linkedWorker.IsRunning ? IconKind.Stop : IconKind.Terminal,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = cardBg,
            ForeColor = linkedWorker != null && linkedWorker.IsRunning ? ModernColors.Success : ModernColors.TextSecondary
        };
        btnWorker.Click += (s, e) =>
        {
            var w = WorkerManager.FindByDomain(site.Domain);
            if (w != null)
            {
                if (w.IsRunning)
                {
                    WorkerManager.StopWorker(w);
                }
                else
                {
                    WorkerManager.StartWorker(w, _appRoot, _job);
                }
                btnWorker.IconKind = w.IsRunning ? IconKind.Stop : IconKind.Terminal;
                btnWorker.ForeColor = w.IsRunning ? ModernColors.Success : ModernColors.TextSecondary;
                RefreshWorkersList();
            }
            else
            {
                using var dlg = new WorkerDialog(_appRoot, null, site);
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultWorker != null)
                {
                    WorkerManager.AddWorker(_appRoot, dlg.ResultWorker);
                    WorkerManager.StartWorker(dlg.ResultWorker, _appRoot, _job);
                    btnWorker.IconKind = dlg.ResultWorker.IsRunning ? IconKind.Stop : IconKind.Terminal;
                    btnWorker.ForeColor = dlg.ResultWorker.IsRunning ? ModernColors.Success : ModernColors.TextSecondary;
                    RefreshWorkersList();
                }
            }
        };
        tip.SetToolTip(btnWorker, linkedWorker != null
            ? (linkedWorker.IsRunning ? $"Stop background worker ({linkedWorker.Name})" : $"Start background worker ({linkedWorker.Name})")
            : $"Run background worker (e.g. npm run dev) for {site.Domain}");

        var btnEdit = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Gear,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = cardBg,
            ForeColor = ModernColors.TextSecondary
        };
        btnEdit.Click += async (s, e) =>
        {
            using var dlg = new SiteEditDialog(_appRoot, _config, site);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                VirtualHostManager.GenerateVhostsConfig(_appRoot, _config, Path.Combine(_nginx.GetNginxDirectory(), "conf"));
                _ = _php.EnsureWorkersForConfiguredSitesAsync();
                if (_nginx.Status == ServiceStatus.Running)
                {
                    await _nginx.RestartAsync();
                }
                RefreshSitesList();
                lblStatusText.Text = $"Site '{site.Domain}' updated.";
                lblStatusText.ForeColor = ModernColors.Success;
            }
        };
        tip.SetToolTip(btnEdit, $"Edit settings for {site.Domain}");

        ModernButton? btnDelete = null;
        if (site.IsExternal)
        {
            btnDelete = new ModernButton
            {
                Text = "",
                IconKind = IconKind.Trash,
                IconSize = 12,
                Width = 28,
                Height = 28,
                BorderRadius = 5,
                ShowBorder = true,
                BorderLineColor = Color.FromArgb(244, 63, 94, 120),
                NormalColor = ModernColors.Card,
                HoverColor = Color.FromArgb(45, 20, 28),
                CustomSurfaceColor = cardBg,
                ForeColor = Color.FromArgb(254, 205, 211)
            };
            btnDelete.Click += async (s, e) =>
            {
                var ans = MessageBox.Show(this,
                    $"Unlink external site '{site.Domain}' from Dev Lite Server?\n\nProject files on disk will not be deleted.",
                    "Remove External Site",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (ans == DialogResult.Yes)
                {
                    VirtualHostManager.DeleteSite(_appRoot, site.Id);
                    VirtualHostManager.GenerateVhostsConfig(_appRoot, _config, Path.Combine(_nginx.GetNginxDirectory(), "conf"));
                    if (_nginx.Status == ServiceStatus.Running)
                    {
                        await _nginx.RestartAsync();
                    }
                    RefreshSitesList();
                    lblStatusText.Text = $"External site '{site.Domain}' unlinked.";
                    lblStatusText.ForeColor = ModernColors.Success;
                }
            };
            tip.SetToolTip(btnDelete, $"Unlink external site '{site.Domain}'");
            card.Controls.Add(btnDelete);
        }

        void LayoutSiteButtons()
        {
            int cardH = card.Height - 10;
            int rightX = card.Width > 200 ? card.Width - btnBrowse.Width - 14 : 500;
            btnBrowse.Location = new Point(rightX, (cardH - btnBrowse.Height) / 2);
            btnOpenDir.Location = new Point(btnBrowse.Left - btnOpenDir.Width - 6, (cardH - btnOpenDir.Height) / 2);
            btnOpenCode.Location = new Point(btnOpenDir.Left - btnOpenCode.Width - 6, (cardH - btnOpenCode.Height) / 2);
            btnShare.Location = new Point(btnOpenCode.Left - btnShare.Width - 6, (cardH - btnShare.Height) / 2);
            btnWorker.Location = new Point(btnShare.Left - btnWorker.Width - 6, (cardH - btnWorker.Height) / 2);
            btnEdit.Location = new Point(btnWorker.Left - btnEdit.Width - 6, (cardH - btnEdit.Height) / 2);

            int leftBoundary = btnEdit.Left;
            if (btnDelete != null)
            {
                btnDelete.Location = new Point(btnEdit.Left - btnDelete.Width - 6, (cardH - btnDelete.Height) / 2);
                leftBoundary = btnDelete.Left;
            }

            int maxTextW = Math.Max(50, leftBoundary - lblName.Left - 12);
            lblName.MaximumSize = new Size(maxTextW, 22);
            lblUrl.MaximumSize = new Size(maxTextW, 20);
        }
        card.Resize += (s, e) => LayoutSiteButtons();
        LayoutSiteButtons();

        card.Controls.Add(lblName);
        card.Controls.Add(lblUrl);
        card.Controls.Add(btnOpenCode);
        card.Controls.Add(btnShare);
        card.Controls.Add(btnWorker);
        card.Controls.Add(btnEdit);
        card.Controls.Add(btnOpenDir);
        card.Controls.Add(btnBrowse);

        return card;
    }

    // ------------------------------------------
    // PAGE 3: PHP
    // ------------------------------------------
    private void PopulatePhpPage()
    {
        pagePhp.Padding = new Padding(24, 14, 24, 16);
        pagePhp.AutoScroll = true;

        // Hero Control Banner (Streamlined horizontal bar, Height: 58 + 8px gap)
        var heroCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 66,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 6, 14, 6)
        };
        heroCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, heroCard.Width - 1f, 58f);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bg = new SolidBrush(ModernColors.Surface);
            g.FillPath(bg, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        _pillPhp = new StatusPill
        {
            Status = _php.Status,
            Location = new Point(14, 17)
        };

        _lblPhpTitle = new Label
        {
            Text = $"PHP {_config.ActivePhp} (Active)",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(116, 7),
            AutoSize = true
        };

        _lblPhpSub = new Label
        {
            Text = "Supervisor Pool: 5000 reqs  •  Laravel Stack Ready",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(117, 31),
            AutoSize = true
        };

        _php.StatusChanged += (svc, st) =>
        {
            if (IsHandleCreated && !IsDisposed)
            {
                try { BeginInvoke(() => { if (_pillPhp != null) _pillPhp.Status = st; }); } catch { }
            }
        };

        // Hidden / internal combo sync
        _cmbPhpVersions = new ModernComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false
        };
        RefreshPhpVersionsCombo();

        var btnRestartPhp = new ModernButton
        {
            Text = "Restart",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 88,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnRestartPhp.Click += async (s, e) =>
        {
            btnRestartPhp.Enabled = false;
            await _php.RestartAsync();
            btnRestartPhp.Enabled = true;
        };

        var btnOpenPhpIni = new ModernButton
        {
            Text = "php.ini",
            IconKind = IconKind.Gear,
            IconSize = 10,
            Width = 74,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnOpenPhpIni.Click += (s, e) =>
        {
            string iniFile = Path.Combine(_php.GetPhpDirectory(), "php.ini");
            if (!File.Exists(iniFile))
            {
                string tpl = Path.Combine(_appRoot, "templates", "php.ini.tpl");
                if (File.Exists(tpl))
                {
                    var vars = TemplateEngine.CreateVariables(_appRoot, _config, Path.Combine(_php.GetPhpDirectory(), "ext"));
                    TemplateEngine.ProcessTemplate(tpl, iniFile, vars);
                }
            }
            if (File.Exists(iniFile))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{iniFile}\"") { UseShellExecute = true });
            }
        };

        var btnOpenExt = new ModernButton
        {
            Text = "Extensions",
            IconKind = IconKind.Lightning,
            IconSize = 10,
            Width = 92,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnOpenExt.Click += (s, e) =>
        {
            using var dlg = new PhpExtensionDialog(_php.GetPhpDirectory(), _config.ActivePhp, _php);
            dlg.ShowDialog(this);
        };

        var lblPortTitle = new Label
        {
            Text = "Port:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            AutoSize = true
        };
        var txtFastCgiPort = CreatePortInput(_config.PhpFastCgiPort, p =>
        {
            _config.PhpFastCgiPort = p;
            _php.UpdatePort(p);
            UpdatePhpBannerText();
        });
        txtFastCgiPort.Width = 56;
        txtFastCgiPort.Height = 28;

        void LayoutHeroControls()
        {
            int rightX = heroCard.ClientSize.Width - 14;
            int btnY = (58 - 28) / 2;

            btnOpenExt.Location = new Point(rightX - btnOpenExt.Width, btnY);
            rightX = btnOpenExt.Left - 6;

            btnOpenPhpIni.Location = new Point(rightX - btnOpenPhpIni.Width, btnY);
            rightX = btnOpenPhpIni.Left - 6;

            btnRestartPhp.Location = new Point(rightX - btnRestartPhp.Width, btnY);
            rightX = btnRestartPhp.Left - 8;

            txtFastCgiPort.Location = new Point(rightX - txtFastCgiPort.Width, btnY);
            rightX = txtFastCgiPort.Left - 4;

            lblPortTitle.Location = new Point(rightX - lblPortTitle.Width, (58 - lblPortTitle.Height) / 2);

            // Constrain Title & Subtitle so they NEVER collide with port / buttons
            int maxTextW = Math.Max(50, lblPortTitle.Left - _lblPhpTitle.Left - 8);
            _lblPhpTitle.AutoEllipsis = true;
            _lblPhpTitle.MaximumSize = new Size(maxTextW, 22);
            _lblPhpSub.AutoEllipsis = true;
            _lblPhpSub.MaximumSize = new Size(maxTextW, 18);
        }

        var heroTip = new ToolTip();
        heroTip.SetToolTip(btnRestartPhp, "Restart PHP FastCGI supervisor");
        heroTip.SetToolTip(btnOpenPhpIni, "Open php.ini configuration");
        heroTip.SetToolTip(btnOpenExt, "Manage PHP extensions in php.ini");
        heroTip.SetToolTip(txtFastCgiPort, "FastCGI listening TCP port");

        heroCard.Resize += (s, e) => LayoutHeroControls();
        heroCard.Controls.AddRange([_pillPhp, _lblPhpTitle, _lblPhpSub, lblPortTitle, txtFastCgiPort, btnRestartPhp, btnOpenPhpIni, btnOpenExt]);
        LayoutHeroControls();

        var cardSqlServer = CreateSqlServerCard();

        // Subheader for installed packages + Install Version button
        var pnlPhpPacksHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(0, 4, 0, 4)
        };
        var lblPhpPacks = new Label
        {
            Text = "Installed PHP Versions",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(0, 8),
            AutoSize = true
        };
        var btnAddPhp = new ModernButton
        {
            Text = "+ Install Version",
            Width = 118,
            Height = 28,
            BorderRadius = 6,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            ForeColor = Color.White,
            ShowBorder = false,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        void LayoutPhpPacksHeader()
        {
            btnAddPhp.Location = new Point(pnlPhpPacksHeader.ClientSize.Width - btnAddPhp.Width - 2, 4);
        }
        pnlPhpPacksHeader.Resize += (s, e) => LayoutPhpPacksHeader();
        LayoutPhpPacksHeader();

        pnlPhpPacksHeader.Controls.AddRange([lblPhpPacks, btnAddPhp]);

        _pnlPhpPackages = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        void RefreshPhpPackagesList()
        {
            _pnlPhpPackages.Controls.Clear();
            var packages = PackageCatalog.GetPhpPackages().Where(p => p.IsInstalled(_appRoot)).ToList();
            if (packages.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No PHP versions installed. Click '+ Install Version' above to download an official runtime.",
                    ForeColor = ModernColors.TextMuted,
                    Font = new Font("Segoe UI", 9f),
                    AutoSize = true,
                    Padding = new Padding(4, 10, 4, 10)
                };
                _pnlPhpPackages.Controls.Add(lblEmpty);
                return;
            }

            var cards = new List<Control>();
            foreach (var pkg in packages)
            {
                var card = new PackageRowCard(
                    pkg,
                    _appRoot,
                    _config,
                    onChanged: () => RefreshPhpPackagesList(),
                    onSwitchAsync: async p => await SwitchActivePhpAsync(p.FolderName)
                );
                cards.Add(card);
            }
            cards.Reverse();
            _pnlPhpPackages.Controls.AddRange(cards.ToArray());
        }

        btnAddPhp.Click += (s, e) =>
        {
            using var dlg = new RuntimeCatalogDialog("PHP", _appRoot, _config, async p =>
            {
                RefreshPhpPackagesList();
                RefreshPhpVersionsCombo();
                await Task.CompletedTask;
            });
            dlg.ShowDialog(this);
            if (dlg.HasInstalledAny)
            {
                RefreshPhpPackagesList();
                RefreshPhpVersionsCombo();
            }
        };

        _refreshPhpPackages = RefreshPhpPackagesList;
        RefreshPhpPackagesList();

        pagePhp.Controls.AddRange([heroCard, cardSqlServer, pnlPhpPacksHeader, _pnlPhpPackages]);
        heroCard.BringToFront();
        cardSqlServer.BringToFront();
        pnlPhpPacksHeader.BringToFront();
        _pnlPhpPackages.BringToFront();
    }

    private Panel CreateSqlServerCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 4, 14, 6)
        };

        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, card.Width - 1f, 56f);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bg = new SolidBrush(ModernColors.Surface);
            g.FillPath(bg, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);

            // Icon Badge (32x32)
            var badgeRect = new RectangleF(12f, 12f, 32f, 32f);
            using var badgePath = CreateRoundedRectangle(badgeRect, 6f);
            using var badgeBg = new SolidBrush(ModernColors.Card);
            using var badgeBorder = new Pen(ModernColors.BorderSubtle, 1f);
            g.FillPath(badgeBg, badgePath);
            g.DrawPath(badgeBorder, badgePath);

            var iconRect = new RectangleF(19f, 19f, 18f, 18f);
            VectorIcons.Draw(g, IconKind.Database, iconRect, ModernColors.Primary);
        };

        var lblTitle = new Label
        {
            Text = "Microsoft SQL Server Drivers",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(56, 7),
            AutoSize = true
        };

        var lblStatus = new Label
        {
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(57, 29),
            AutoSize = true
        };

        var tip = new ToolTip();
        tip.SetToolTip(lblTitle, "Microsoft SQL Server & Azure SQL database drivers for PHP");

        var btnInstallSqlSrv = new ModernButton
        {
            Width = 110,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        var btnInstallOdbc = new ModernButton
        {
            Width = 115,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        void RefreshCardState()
        {
            string phpDir = _php.GetPhpDirectory();
            bool isPhpSrvInstalled = SqlServerDriverManager.IsPhpDriverInstalled(phpDir);
            bool isOdbcInstalled = SqlServerDriverManager.IsOdbcDriverInstalled(out string? odbcName);

            string srvStatus = isPhpSrvInstalled ? "sqlsrv: Ready" : "sqlsrv: Not Installed";
            string odbcStatus = isOdbcInstalled ? $"{odbcName ?? "ODBC Driver"} Ready" : "MS ODBC: Missing";
            lblStatus.Text = $"{srvStatus}  •  {odbcStatus}";

            if (isPhpSrvInstalled)
            {
                btnInstallSqlSrv.Text = "Remove sqlsrv";
                btnInstallSqlSrv.IconKind = IconKind.Trash;
                btnInstallSqlSrv.ForeColor = ModernColors.Danger;
                btnInstallSqlSrv.NormalColor = ModernColors.Card;
                btnInstallSqlSrv.HoverColor = Color.FromArgb(40, 20, 30);
                tip.SetToolTip(btnInstallSqlSrv, "Remove sqlsrv extension from active PHP");
            }
            else
            {
                btnInstallSqlSrv.Text = "Install sqlsrv";
                btnInstallSqlSrv.IconKind = IconKind.Download;
                btnInstallSqlSrv.ForeColor = ModernColors.Primary;
                btnInstallSqlSrv.NormalColor = ModernColors.Card;
                btnInstallSqlSrv.HoverColor = ModernColors.SurfaceHover;
                tip.SetToolTip(btnInstallSqlSrv, "Download and install sqlsrv extension for active PHP");
            }

            if (isOdbcInstalled)
            {
                btnInstallOdbc.Text = "ODBC Ready";
                btnInstallOdbc.IconKind = IconKind.Check;
                btnInstallOdbc.ForeColor = ModernColors.Success;
                btnInstallOdbc.Enabled = false;
                btnInstallOdbc.NormalColor = ModernColors.Card;
                tip.SetToolTip(btnInstallOdbc, $"{odbcName ?? "ODBC Driver"} is installed and ready");
            }
            else
            {
                btnInstallOdbc.Text = "Install ODBC 18";
                btnInstallOdbc.IconKind = IconKind.Download;
                btnInstallOdbc.ForeColor = ModernColors.Warning;
                btnInstallOdbc.Enabled = true;
                btnInstallOdbc.NormalColor = ModernColors.Card;
                btnInstallOdbc.HoverColor = ModernColors.SurfaceHover;
                tip.SetToolTip(btnInstallOdbc, "Download and install Microsoft ODBC Driver 18 for Windows");
            }

            int right = card.ClientSize.Width - 14;
            int btnY = (56 - 28) / 2;
            btnInstallOdbc.Location = new Point(right - btnInstallOdbc.Width, btnY);
            btnInstallSqlSrv.Location = new Point(btnInstallOdbc.Left - btnInstallSqlSrv.Width - 8, btnY);

            int maxTextW = Math.Max(50, btnInstallSqlSrv.Left - lblTitle.Left - 8);
            lblTitle.AutoEllipsis = true;
            lblTitle.MaximumSize = new Size(maxTextW, 20);
            lblStatus.AutoEllipsis = true;
            lblStatus.MaximumSize = new Size(maxTextW, 18);
        }

        _refreshSqlServerCard = RefreshCardState;

        btnInstallSqlSrv.Click += async (s, e) =>
        {
            btnInstallSqlSrv.Enabled = false;
            string phpDir = _php.GetPhpDirectory();
            bool isInstalled = SqlServerDriverManager.IsPhpDriverInstalled(phpDir);

            if (isInstalled)
            {
                SqlServerDriverManager.RemovePhpDrivers(phpDir);
                if (_php.Status == ServiceStatus.Running)
                {
                    await _php.RestartAsync();
                }
                lblStatusText.Text = "Removed SQL Server drivers from PHP.";
            }
            else
            {
                lblStatusText.Text = "Downloading SQL Server drivers...";
                string tempDir = Path.Combine(_appRoot, "build", "temp_downloads");
                var (success, msg) = await SqlServerDriverManager.InstallPhpDriversAsync(
                    phpDir,
                    _config.ActivePhp,
                    tempDir,
                    new Progress<string>(p => lblStatusText.Text = p));

                lblStatusText.Text = msg;
                if (success && _php.Status == ServiceStatus.Running)
                {
                    await _php.RestartAsync();
                }
            }

            btnInstallSqlSrv.Enabled = true;
            RefreshCardState();
        };

        btnInstallOdbc.Click += async (s, e) =>
        {
            btnInstallOdbc.Enabled = false;
            lblStatusText.Text = "Downloading Microsoft ODBC Driver 18...";
            string tempDir = Path.Combine(_appRoot, "build", "temp_downloads");
            var (success, msg) = await SqlServerDriverManager.InstallOdbcDriverAsync(
                tempDir,
                new Progress<string>(p => lblStatusText.Text = p));

            lblStatusText.Text = msg;
            btnInstallOdbc.Enabled = true;
            RefreshCardState();
        };

        card.Resize += (s, e) =>
        {
            int right = card.ClientSize.Width - 16;
            btnInstallOdbc.Location = new Point(right - btnInstallOdbc.Width, 24);
            btnInstallSqlSrv.Location = new Point(btnInstallOdbc.Left - btnInstallSqlSrv.Width - 8, 24);

            int maxW = btnInstallSqlSrv.Left - lblTitle.Left - 10;
            if (maxW > 50)
            {
                lblTitle.MaximumSize = new Size(maxW, 20);
                lblStatus.MaximumSize = new Size(maxW, 20);
                lblTitle.AutoEllipsis = true;
                lblStatus.AutoEllipsis = true;
            }
        };

        card.Controls.Add(lblTitle);
        card.Controls.Add(lblStatus);
        card.Controls.Add(btnInstallSqlSrv);
        card.Controls.Add(btnInstallOdbc);

        RefreshCardState();
        return card;
    }

    // ------------------------------------------
    // PAGE 4: NODE
    // ------------------------------------------
    private void PopulateNodePage()
    {
        pageNode.Padding = new Padding(24, 16, 24, 20);
        pageNode.AutoScroll = true;

        // ------------------------------------------
        // Hero Card: Active Runtime Status
        // ------------------------------------------
        var heroCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 6, 14, 6)
        };
        heroCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, heroCard.Width - 1f, heroCard.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bg = new SolidBrush(ModernColors.Surface);
            g.FillPath(bg, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        var (sysFound, sysPath, sysVer) = NodeManager.DetectSystemNode();

        string GetNodeTitleText()
        {
            if (_config.ActiveNode.StartsWith("system:", StringComparison.OrdinalIgnoreCase))
            {
                string p = _config.ActiveNode.Substring("system:".Length);
                string exe = Path.Combine(p, "node.exe");
                string v = File.Exists(exe) ? NodeManager.GetNodeVersion(exe) : Path.GetFileName(p);
                return $"Active: Windows Node ({v})";
            }
            if (_config.ActiveNode.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                return sysFound ? $"Active: System Node ({sysVer})" : "Active: System Node (Windows Global)";
            }
            return $"Active: LiteServer Node.js {_config.ActiveNode}";
        }

        var pillNode = new StatusPill
        {
            Status = ServiceStatus.Running,
            Location = new Point(14, 17)
        };

        _lblNodeTitle = new Label
        {
            Text = GetNodeTitleText(),
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(116, 7),
            AutoSize = true
        };

        var lblNodeSub = new Label
        {
            Text = "Linked to bin\\nodejs\\current  •  NPM & NPX Ready",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(117, 31),
            AutoSize = true
        };

        bool isCurrentSystem = _config.ActiveNode.StartsWith("system", StringComparison.OrdinalIgnoreCase);
        bool showSystemMode = isCurrentSystem;

        void UpdateHeroBanner()
        {
            _lblNodeTitle.Text = GetNodeTitleText();
            bool isSysActive = _config.ActiveNode.StartsWith("system", StringComparison.OrdinalIgnoreCase);
            if (!showSystemMode)
            {
                lblNodeSub.Text = !isSysActive
                    ? "LiteServer Portable active  •  Linked to bin\\nodejs\\current  •  NPM & NPX Ready"
                    : "Windows System Node is active  •  Select a LiteServer version below to switch";
            }
            else
            {
                lblNodeSub.Text = isSysActive
                    ? "Windows System Node active  •  Linked to bin\\nodejs\\current  •  NPM & NPX Ready"
                    : "LiteServer Portable is active  •  Select a Windows Node.js installation below to switch";
            }
        }

        var btnOpenNodeTerminal = new ModernButton
        {
            Text = "Terminal",
            IconKind = IconKind.Terminal,
            IconSize = 11,
            Width = 100,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnOpenNodeTerminal.Click += BtnOpenTerminal_Click;

        var btnOpenNodeFolder = new ModernButton
        {
            Text = "Directory",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 95,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnOpenNodeFolder.Click += (s, e) =>
        {
            string nodeDir;
            if (_config.ActiveNode.StartsWith("system:", StringComparison.OrdinalIgnoreCase))
            {
                nodeDir = _config.ActiveNode.Substring("system:".Length);
            }
            else if (_config.ActiveNode.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                var (sf, sp, _) = NodeManager.DetectSystemNode();
                nodeDir = sf ? sp : Path.Combine(_appRoot, "bin", "nodejs");
            }
            else
            {
                nodeDir = Path.Combine(_appRoot, "bin", "nodejs", _config.ActiveNode);
                if (!Directory.Exists(nodeDir)) nodeDir = Path.Combine(_appRoot, "bin", "nodejs");
            }
            if (Directory.Exists(nodeDir))
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{nodeDir}\"") { UseShellExecute = true }); } catch { }
            }
        };

        void LayoutHeroControls()
        {
            int rightX = heroCard.ClientSize.Width - 14;
            int btnY = (heroCard.Height - 28) / 2;

            btnOpenNodeFolder.Location = new Point(rightX - btnOpenNodeFolder.Width, btnY);
            rightX = btnOpenNodeFolder.Left - 6;

            btnOpenNodeTerminal.Location = new Point(rightX - btnOpenNodeTerminal.Width, btnY);

            int maxTextW = Math.Max(50, btnOpenNodeTerminal.Left - _lblNodeTitle.Left - 8);
            _lblNodeTitle.AutoEllipsis = true;
            _lblNodeTitle.MaximumSize = new Size(maxTextW, 22);
            lblNodeSub.AutoEllipsis = true;
            lblNodeSub.MaximumSize = new Size(maxTextW, 18);
        }

        heroCard.Resize += (s, e) => LayoutHeroControls();
        heroCard.Controls.AddRange([pillNode, _lblNodeTitle, lblNodeSub, btnOpenNodeTerminal, btnOpenNodeFolder]);
        LayoutHeroControls();

        // ------------------------------------------
        // Mode Selector: Tabs directly above version lists
        // ------------------------------------------
        var pnlModeSelector = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 10, 0, 6),
            Padding = new Padding(0, 6, 0, 4)
        };

        var btnModeLiteServer = new ModernButton
        {
            Text = "LiteServer Portable",
            IconKind = IconKind.Server,
            IconSize = 11,
            Width = 160,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(0, 4)
        };

        var btnModeSystem = new ModernButton
        {
            Text = "Windows System Node",
            IconKind = IconKind.Terminal,
            IconSize = 11,
            Width = 175,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(168, 4)
        };

        pnlModeSelector.Controls.AddRange([btnModeLiteServer, btnModeSystem]);

        // ------------------------------------------
        // Dynamic Version List Container
        // ------------------------------------------
        var pnlContentContainer = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        var pnlLiteServerView = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        var pnlSystemView = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        pnlContentContainer.Controls.AddRange([pnlSystemView, pnlLiteServerView]);

        // Section A: LiteServer Portable Content
        var pnlNodePacksHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(0, 4, 0, 4)
        };
        var lblNodePacks = new Label
        {
            Text = "Installed LiteServer Runtimes",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(0, 8),
            AutoSize = true
        };
        var btnAddNode = new ModernButton
        {
            Text = "+ Install Version",
            Width = 118,
            Height = 28,
            BorderRadius = 6,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            ForeColor = Color.White,
            ShowBorder = false,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        void LayoutNodePacksHeader()
        {
            btnAddNode.Location = new Point(pnlNodePacksHeader.ClientSize.Width - btnAddNode.Width - 2, 4);
        }
        pnlNodePacksHeader.Resize += (s, e) => LayoutNodePacksHeader();
        LayoutNodePacksHeader();

        pnlNodePacksHeader.Controls.AddRange([lblNodePacks, btnAddNode]);

        _pnlNodePackages = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        Action? refreshSystemNodeList = null;

        void RefreshNodePackagesList()
        {
            _pnlNodePackages.Controls.Clear();
            var packages = PackageCatalog.GetNodePackages().Where(p => p.IsInstalled(_appRoot)).ToList();
            if (packages.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No portable Node.js versions installed. Click '+ Install Version' above to download one.",
                    ForeColor = ModernColors.TextMuted,
                    Font = new Font("Segoe UI", 9f),
                    AutoSize = true,
                    Padding = new Padding(4, 10, 4, 10)
                };
                _pnlNodePackages.Controls.Add(lblEmpty);
                return;
            }

            var cards = new List<Control>();
            foreach (var pkg in packages)
            {
                var card = new PackageRowCard(
                    pkg,
                    _appRoot,
                    _config,
                    onChanged: () => RefreshNodePackagesList(),
                    onSwitchAsync: async p =>
                    {
                        _config.ActiveNode = p.FolderName;
                        ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                        NodeManager.UpdateCurrentJunction(_appRoot, p.FolderName);
                        UpdateHeroBanner();
                        RefreshNodePackagesList();
                        refreshSystemNodeList?.Invoke();
                        BuildTrayMenu();
                        lblStatusText.Text = $"Node.js {p.FolderName} active.";
                        await Task.CompletedTask;
                    }
                );
                cards.Add(card);
            }
            cards.Reverse();
            _pnlNodePackages.Controls.AddRange(cards.ToArray());
        }

        btnAddNode.Click += (s, e) =>
        {
            using var dlg = new RuntimeCatalogDialog("Node.js", _appRoot, _config, async p =>
            {
                RefreshNodePackagesList();
                await Task.CompletedTask;
            });
            dlg.ShowDialog(this);
            if (dlg.HasInstalledAny)
            {
                RefreshNodePackagesList();
            }
        };

        pnlLiteServerView.Controls.AddRange([pnlNodePacksHeader, _pnlNodePackages]);
        pnlNodePacksHeader.BringToFront();
        _pnlNodePackages.BringToFront();

        // Section B: Windows System Node Content
        var pnlSystemHeader = CreateSubSectionHeader("Detected Windows Node.js Installations");

        var pnlSystemList = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        pnlSystemView.Controls.AddRange([pnlSystemHeader, pnlSystemList]);
        pnlSystemHeader.BringToFront();
        pnlSystemList.BringToFront();

        void SetNodeMode(bool systemMode)
        {
            showSystemMode = systemMode;

            if (!showSystemMode)
            {
                btnModeLiteServer.NormalColor = Color.FromArgb(16, 40, 56);
                btnModeLiteServer.HoverColor = Color.FromArgb(20, 56, 80);
                btnModeLiteServer.ForeColor = ModernColors.Primary;
                btnModeLiteServer.BorderLineColor = Color.FromArgb(56, 189, 248, 140);

                btnModeSystem.NormalColor = ModernColors.Card;
                btnModeSystem.HoverColor = ModernColors.SurfaceHover;
                btnModeSystem.ForeColor = ModernColors.TextSecondary;
                btnModeSystem.BorderLineColor = ModernColors.BorderSubtle;

                pnlLiteServerView.Visible = true;
                pnlSystemView.Visible = false;
            }
            else
            {
                btnModeLiteServer.NormalColor = ModernColors.Card;
                btnModeLiteServer.HoverColor = ModernColors.SurfaceHover;
                btnModeLiteServer.ForeColor = ModernColors.TextSecondary;
                btnModeLiteServer.BorderLineColor = ModernColors.BorderSubtle;

                btnModeSystem.NormalColor = Color.FromArgb(16, 40, 56);
                btnModeSystem.HoverColor = Color.FromArgb(20, 56, 80);
                btnModeSystem.ForeColor = ModernColors.Primary;
                btnModeSystem.BorderLineColor = Color.FromArgb(56, 189, 248, 140);

                pnlLiteServerView.Visible = false;
                pnlSystemView.Visible = true;
            }

            UpdateHeroBanner();
        }

        btnModeLiteServer.Click += (s, e) => SetNodeMode(false);
        btnModeSystem.Click += (s, e) => SetNodeMode(true);

        Panel CreateSystemItemCard(string title, string version, string path, bool isGlobal)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(14, 4, 14, 6)
            };

            bool isActive;
            if (isGlobal)
            {
                isActive = _config.ActiveNode.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                           (_config.ActiveNode.StartsWith("system:", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(_config.ActiveNode.Substring("system:".Length), path, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                isActive = _config.ActiveNode.StartsWith("system:", StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(_config.ActiveNode.Substring("system:".Length), path, StringComparison.OrdinalIgnoreCase);
            }

            var pill = new StatusPill
            {
                Status = isActive ? ServiceStatus.Running : ServiceStatus.Stopped,
                Location = new Point(14, 13)
            };

            var lblTitle = new Label
            {
                Text = title,
                ForeColor = ModernColors.TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(116, 7),
                AutoSize = true
            };

            var lblTag = new Label
            {
                Text = $"{version}  •  {path}",
                ForeColor = isActive ? ModernColors.Primary : ModernColors.TextMuted,
                Font = new Font("Segoe UI", 8.25f),
                Location = new Point(117, 29),
                AutoSize = true
            };

            var btnSwitch = new ModernButton
            {
                Text = isActive ? "Active" : "Use",
                IconKind = isActive ? IconKind.Check : IconKind.Play,
                IconSize = 10,
                Width = 76,
                Height = 28,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = isActive ? Color.FromArgb(16, 40, 32) : ModernColors.Card,
                HoverColor = isActive ? Color.FromArgb(16, 40, 32) : ModernColors.SurfaceHover,
                ForeColor = isActive ? ModernColors.Success : ModernColors.TextPrimary,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Enabled = !isActive,
                Cursor = isActive ? Cursors.Default : Cursors.Hand
            };

            var btnFolder = new ModernButton
            {
                Text = "",
                IconKind = IconKind.Folder,
                IconSize = 11,
                Width = 32,
                Height = 28,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.TextSecondary,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
            };

            var tip = new ToolTip();
            tip.SetToolTip(btnSwitch, isActive ? $"{title} is currently active" : $"Switch active runtime to {title}");
            tip.SetToolTip(btnFolder, $"Open folder: {path}");

            btnFolder.Click += (s, e) =>
            {
                if (Directory.Exists(path))
                {
                    try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); } catch { }
                }
            };

            btnSwitch.Click += (s, e) =>
            {
                string newActive = isGlobal ? "system" : $"system:{path}";
                _config.ActiveNode = newActive;
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                NodeManager.UpdateCurrentJunction(_appRoot, newActive);
                UpdateHeroBanner();
                refreshSystemNodeList?.Invoke();
                RefreshNodePackagesList();
                BuildTrayMenu();
                lblStatusText.Text = $"Windows Node.js ({version}) active.";
            };

            void LayoutItemControls()
            {
                int rightX = card.ClientSize.Width - 14;
                int btnY = (card.Height - 6 - 28) / 2;

                btnFolder.Location = new Point(rightX - btnFolder.Width, btnY);
                rightX = btnFolder.Left - 6;

                btnSwitch.Location = new Point(rightX - btnSwitch.Width, btnY);
                rightX = btnSwitch.Left - 8;

                int maxTextW = Math.Max(50, rightX - lblTitle.Left - 8);
                lblTitle.AutoEllipsis = true;
                lblTitle.MaximumSize = new Size(maxTextW, 20);
                lblTag.AutoEllipsis = true;
                lblTag.MaximumSize = new Size(maxTextW, 18);
            }

            card.Resize += (s, e) => LayoutItemControls();

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float stroke = 1f;
                float halfStroke = stroke / 2f;
                float cardH = card.Height - 6f;
                var rect = new RectangleF(halfStroke, halfStroke, card.Width - stroke, cardH - stroke);
                using var pathShape = CreateRoundedRectangle(rect, 6f);
                using var bg = new SolidBrush(ModernColors.Surface);
                g.FillPath(bg, pathShape);
                using var pen = new Pen(isActive ? Color.FromArgb(56, 189, 248, 120) : ModernColors.BorderSubtle, stroke);
                g.DrawPath(pen, pathShape);

                if (isActive)
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

            card.Controls.AddRange([pill, lblTitle, lblTag, btnSwitch, btnFolder]);
            LayoutItemControls();
            return card;
        }

        Panel CreateEmptySystemNodeCard()
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(16, 10, 16, 10)
            };

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new RectangleF(0.5f, 0.5f, card.Width - 1f, card.Height - 1f);
                using var path = CreateRoundedRectangle(rect, 6f);
                using var bg = new SolidBrush(ModernColors.Surface);
                g.FillPath(bg, path);
                using var pen = new Pen(ModernColors.BorderSubtle, 1f);
                g.DrawPath(pen, path);
            };

            var lblNone = new Label
            {
                Text = "No Windows Node.js installation detected in Program Files, AppData, or PATH.",
                ForeColor = ModernColors.TextSecondary,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(14, 12),
                AutoSize = true
            };

            var lblHint = new Label
            {
                Text = "Install Node.js globally on Windows or switch back to LiteServer Portable runtimes.",
                ForeColor = ModernColors.TextMuted,
                Font = new Font("Segoe UI", 8.25f),
                Location = new Point(14, 34),
                AutoSize = true
            };

            var btnBack = new ModernButton
            {
                Text = "Use LiteServer Portable",
                IconKind = IconKind.Server,
                IconSize = 10,
                Width = 165,
                Height = 28,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.Primary,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
            };
            btnBack.Click += (s, e) => SetNodeMode(false);

            void LayoutEmptyCard()
            {
                btnBack.Location = new Point(card.ClientSize.Width - 14 - btnBack.Width, (card.Height - btnBack.Height) / 2);
                int maxW = btnBack.Left - lblNone.Left - 8;
                lblNone.AutoEllipsis = true;
                lblNone.MaximumSize = new Size(maxW, 20);
                lblHint.AutoEllipsis = true;
                lblHint.MaximumSize = new Size(maxW, 18);
            }

            card.Resize += (s, e) => LayoutEmptyCard();
            card.Controls.AddRange([lblNone, lblHint, btnBack]);
            LayoutEmptyCard();
            return card;
        }

        refreshSystemNodeList = () =>
        {
            pnlSystemList.Controls.Clear();
            var detectedList = new List<(string name, string version, string path, bool isGlobal)>();

            var (sf, sp, sv) = NodeManager.DetectSystemNode();
            if (sf)
            {
                detectedList.Add(("System Node.js (Windows Global)", sv, sp, true));
            }

            var nvmVersions = NodeManager.DetectNvmVersions();
            foreach (var (nvmVer, nvmPath) in nvmVersions)
            {
                if (sf && string.Equals(sp, nvmPath, StringComparison.OrdinalIgnoreCase)) continue;
                detectedList.Add(($"Node.js {nvmVer} (NVM)", nvmVer, nvmPath, false));
            }

            if (detectedList.Count == 0)
            {
                pnlSystemList.Controls.Add(CreateEmptySystemNodeCard());
                return;
            }

            var cards = new List<Control>();
            foreach (var item in detectedList)
            {
                cards.Add(CreateSystemItemCard(item.name, item.version, item.path, item.isGlobal));
            }
            pnlSystemList.Controls.AddRange(cards.ToArray());
        };

        _refreshNodePackages = () =>
        {
            UpdateHeroBanner();
            RefreshNodePackagesList();
            refreshSystemNodeList();
        };

        RefreshNodePackagesList();
        refreshSystemNodeList();
        SetNodeMode(isCurrentSystem);

        pageNode.Controls.AddRange([
            heroCard,
            pnlModeSelector,
            pnlContentContainer
        ]);

        heroCard.BringToFront();
        pnlModeSelector.BringToFront();
        pnlContentContainer.BringToFront();
    }

    // ------------------------------------------
    // PAGE 5: SERVICES (Databases & Cache)
    // ------------------------------------------
    private void PopulateServicesPage()
    {
        pageServices.Padding = new Padding(24, 16, 24, 20);

        // 1. MySQL Card
        var cardMySql = CreateServiceManagementCard(
            _mysql,
            "MySQL 8.4 Server (Default user: 'root' with empty password)",
            () => _config.EnableMysql,
            v => _config.EnableMysql = v,
            () => _config.MysqlPort,
            p => _config.MysqlPort = p,
            Path.Combine(_appRoot, "bin", "mysql", "data"),
            (force) => OpenDatabaseClient("mysql", force));

        // 2. PostgreSQL Card
        var cardPg = CreateServiceManagementCard(
            _postgresql,
            "PostgreSQL 17 Server (Default user: 'postgres' with trust auth)",
            () => _config.EnablePostgresql,
            v => _config.EnablePostgresql = v,
            () => _config.PostgreSqlPort,
            p => _config.PostgreSqlPort = p,
            Path.Combine(_appRoot, "bin", "postgresql", "data"),
            (force) => OpenDatabaseClient("postgresql", force));

        // 3. Redis Card
        var cardRedis = CreateServiceManagementCard(
            _redis,
            "In-memory key-value cache server binding to loopback 127.0.0.1",
            () => _config.EnableRedis,
            v => _config.EnableRedis = v,
            () => _config.RedisPort,
            p => _config.RedisPort = p,
            null,
            (force) => OpenDatabaseClient("redis", force));

        pageServices.Controls.AddRange([cardMySql, cardPg, cardRedis]);
        cardMySql.BringToFront();
        cardPg.BringToFront();
        cardRedis.BringToFront();
    }

    private Panel CreateServiceManagementCard(
        IService service,
        string description,
        Func<bool> getEnabled,
        Action<bool> setEnabled,
        Func<int> getPort,
        Action<int> setPort,
        string? dataDirPath,
        Action<bool> onOpenClient)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 6, 16, 16)
        };
        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = card.Height - 10f;
            var rect = new RectangleF(halfStroke, halfStroke, card.Width - stroke, cardH - stroke);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var bgBrush = new SolidBrush(ModernColors.Surface);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        var pill = new StatusPill
        {
            Status = service.Status,
            Location = new Point(14, 20)
        };
        service.StatusChanged += (s, st) =>
        {
            if (_isShuttingDown || IsDisposed || !IsHandleCreated || pill.IsDisposed) return;
            if (InvokeRequired) try { BeginInvoke(() => { if (!_isShuttingDown && !pill.IsDisposed) pill.Status = st; }); } catch { }
            else pill.Status = st;
        };

        var lblName = new Label
        {
            Text = service.Name,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = new Point(116, 10),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = description,
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(117, 32),
            AutoSize = true
        };

        // Configuration Row (Y=58): Enable, Port, Auto-start
        var lblEnable = new Label
        {
            Text = "Enabled:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(117, 60),
            AutoSize = true
        };
        var togEnable = new ModernToggle
        {
            Checked = getEnabled(),
            Location = new Point(170, 58)
        };
        togEnable.CheckedChanged += (s, e) =>
        {
            setEnabled(togEnable.Checked);
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            BuildServiceCards();
            BuildTrayMenu();
        };

        var lblPort = new Label
        {
            Text = "Port:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(226, 60),
            AutoSize = true
        };
        var txtPort = CreatePortInput(getPort(), p =>
        {
            setPort(p);
            service.UpdatePort(p);
        });
        txtPort.Location = new Point(260, 57);

        var btnToggle = new ModernButton
        {
            Text = service.Status == ServiceStatus.Running ? "Stop" : (service.Status == ServiceStatus.NotInstalled ? "Install" : "Start"),
            IconKind = service.Status == ServiceStatus.Running ? IconKind.Stop : (service.Status == ServiceStatus.NotInstalled ? IconKind.Download : IconKind.Play),
            IconSize = 10,
            Width = 80,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = service.Status == ServiceStatus.Running ? ModernColors.Danger : (service.Status == ServiceStatus.NotInstalled ? ModernColors.Primary : ModernColors.Success),
            HoverColor = service.Status == ServiceStatus.Running ? ModernColors.DangerHover : (service.Status == ServiceStatus.NotInstalled ? ModernColors.PrimaryHover : ModernColors.SuccessHover),
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        void UpdateBtn()
        {
            if (service.Status == ServiceStatus.Running)
            {
                btnToggle.Text = "Stop";
                btnToggle.IconKind = IconKind.Stop;
                btnToggle.NormalColor = ModernColors.Danger;
                btnToggle.HoverColor = ModernColors.DangerHover;
            }
            else if (service.Status == ServiceStatus.NotInstalled)
            {
                btnToggle.Text = "Install";
                btnToggle.IconKind = IconKind.Download;
                btnToggle.NormalColor = ModernColors.Primary;
                btnToggle.HoverColor = ModernColors.PrimaryHover;
            }
            else
            {
                btnToggle.Text = "Start";
                btnToggle.IconKind = IconKind.Play;
                btnToggle.NormalColor = ModernColors.Success;
                btnToggle.HoverColor = ModernColors.SuccessHover;
            }
        }

        btnToggle.Click += async (s, e) =>
        {
            btnToggle.Enabled = false;
            try
            {
                if (service.Status == ServiceStatus.NotInstalled)
                {
                    string key = service.Name.ToLowerInvariant();
                    if (key.Contains("mysql")) await InstallServiceWithUiAsync("mysql", service);
                    else if (key.Contains("postgre")) await InstallServiceWithUiAsync("postgresql", service);
                    else if (key.Contains("redis")) await InstallServiceWithUiAsync("redis", service);
                    else if (key.Contains("mailpit")) await InstallServiceWithUiAsync("mailpit", service);
                }
                else if (service.Status == ServiceStatus.Running)
                {
                    await service.StopAsync();
                }
                else
                {
                    await service.StartAsync();
                }
            }
            finally
            {
                btnToggle.Enabled = true;
                UpdateBtn();
            }
        };

        service.StatusChanged += (s, st) =>
        {
            if (_isShuttingDown || IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) try { BeginInvoke((Action)(() => { if (!_isShuttingDown && !IsDisposed) UpdateBtn(); })); } catch { }
            else UpdateBtn();
        };

        var btnClient = new ModernButton
        {
            Text = "DB Client",
            IconKind = IconKind.Database,
            IconSize = 10,
            Width = 84,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnClient.Click += (s, e) => onOpenClient((ModifierKeys & Keys.Shift) == Keys.Shift);
        btnClient.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right) onOpenClient(true);
        };
        var ttClient = new ToolTip();
        ttClient.SetToolTip(btnClient, "Open database client\n(Right-click or Shift+Click to choose another client)");

        ModernButton? btnDataDir = null;
        if (!string.IsNullOrEmpty(dataDirPath))
        {
            btnDataDir = new ModernButton
            {
                Text = "Data",
                IconKind = IconKind.Folder,
                IconSize = 10,
                Width = 64,
                Height = 32,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.TextSecondary,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
            };
            btnDataDir.Click += (s, e) =>
            {
                if (!Directory.Exists(dataDirPath)) Directory.CreateDirectory(dataDirPath);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dataDirPath}\"") { UseShellExecute = true });
            };
            card.Controls.Add(btnDataDir);
        }

        void LayoutServiceButtons()
        {
            int cardH = card.Height - 10;
            int rightX = card.Width > 200 ? card.Width - btnToggle.Width - 14 : 500;
            btnToggle.Location = new Point(rightX, (cardH - btnToggle.Height) / 2);
            btnClient.Location = new Point(btnToggle.Left - btnClient.Width - 8, (cardH - btnClient.Height) / 2);
            if (btnDataDir != null)
            {
                btnDataDir.Location = new Point(btnClient.Left - btnDataDir.Width - 8, (cardH - btnDataDir.Height) / 2);
            }

            int leftLimit = (btnDataDir != null ? btnDataDir.Left : btnClient.Left) - 10;
            int maxDescW = leftLimit - lblDesc.Left;
            if (maxDescW > 50)
            {
                lblName.MaximumSize = new Size(maxDescW, 22);
                lblName.AutoEllipsis = true;
                lblDesc.MaximumSize = new Size(maxDescW, 20);
                lblDesc.AutoEllipsis = true;
            }
        }
        card.Resize += (s, e) => LayoutServiceButtons();
        LayoutServiceButtons();

        card.Controls.Add(pill);
        card.Controls.Add(lblName);
        card.Controls.Add(lblDesc);
        card.Controls.Add(lblEnable);
        card.Controls.Add(togEnable);
        card.Controls.Add(lblPort);
        card.Controls.Add(txtPort);
        card.Controls.Add(btnClient);
        card.Controls.Add(btnToggle);

        return card;
    }

    // ------------------------------------------
    // PAGE 5: SERVICES (Mailpit & RustFS S3 Mock)
    // ------------------------------------------
    private void PopulateDevServicesPage()
    {
        pageDevServices.Padding = new Padding(24, 16, 24, 20);
        var serviceTip = new ToolTip { InitialDelay = 250, AutoPopDelay = 12000 };

        string BuildMailpitEnv() =>
            $"MAIL_MAILER=smtp\r\nMAIL_HOST=127.0.0.1\r\nMAIL_PORT={_config.MailpitSmtpPort}\r\nMAIL_USERNAME=null\r\nMAIL_PASSWORD=null\r\nMAIL_ENCRYPTION=null";

        string BuildRustFsEnv() =>
            $"FILESYSTEM_DISK=s3\r\nAWS_ACCESS_KEY_ID={_config.RustFsAccessKey}\r\nAWS_SECRET_ACCESS_KEY={_config.RustFsSecretKey}\r\nAWS_DEFAULT_REGION=us-east-1\r\nAWS_BUCKET=local\r\nAWS_ENDPOINT=http://127.0.0.1:{_config.RustFsApiPort}\r\nAWS_USE_PATH_STYLE_ENDPOINT=true";

        var cardMailpit = CreateDevServiceCard(
            service: _mailpit,
            packageKey: "mailpit",
            title: "Mailpit",
            description: "Local SMTP test capture and webmail inbox inspector.",
            getEnabled: () => _config.EnableMailpit,
            setEnabled: v => _config.EnableMailpit = v,
            port1Label: "SMTP:",
            getPort1: () => _config.MailpitSmtpPort,
            setPort1: p => _config.MailpitSmtpPort = p,
            port2Label: "Web:",
            getPort2: () => _config.MailpitWebPort,
            setPort2: p => { _config.MailpitWebPort = p; _mailpit.UpdatePort(p); },
            uiButtonPrefix: "Inbox",
            uiButtonIcon: IconKind.Mail,
            onOpenUi: () => BtnOpenMailpit_Click(this, EventArgs.Empty),
            getEnvSnippet: BuildMailpitEnv,
            toolTip: serviceTip,
            extraButtonText: "Test",
            extraButtonTooltip: "Send a test SMTP email to Mailpit",
            onExtraAction: async btn =>
            {
                btn.Enabled = false;
                try
                {
                    using var tcp = new System.Net.Sockets.TcpClient();
                    await tcp.ConnectAsync("127.0.0.1", _config.MailpitSmtpPort);
                    using var stream = tcp.GetStream();
                    using var reader = new StreamReader(stream);
                    using var writer = new StreamWriter(stream) { AutoFlush = true };
                    await reader.ReadLineAsync();
                    await writer.WriteLineAsync("HELO localhost");
                    await reader.ReadLineAsync();
                    await writer.WriteLineAsync("MAIL FROM:<noreply@liteserver.local>");
                    await reader.ReadLineAsync();
                    await writer.WriteLineAsync("RCPT TO:<dev@liteserver.local>");
                    await reader.ReadLineAsync();
                    await writer.WriteLineAsync("DATA");
                    await reader.ReadLineAsync();
                    await writer.WriteLineAsync($"Subject: Dev Lite Server - Test Mail ({DateTime.Now:HH:mm:ss})\r\nFrom: noreply@liteserver.local\r\nTo: dev@liteserver.local\r\n\r\nHello from Dev Lite Server Mailpit!\r\nSent successfully at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.\r\n.");
                    await reader.ReadLineAsync();
                    await writer.WriteLineAsync("QUIT");
                    MessageBox.Show(this, "Test email captured successfully! Check your Mailpit Inbox.", "Mailpit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to send test email:\r\n{ex.Message}\r\n\r\nEnsure Mailpit is running on SMTP port {_config.MailpitSmtpPort}.", "Mailpit Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    btn.Enabled = true;
                }
            });

        var cardRustFs = CreateDevServiceCard(
            service: _rustfs,
            packageKey: "rustfs",
            title: "RustFS (S3 Mock)",
            description: $"Local S3-compatible storage mock ({_config.RustFsAccessKey} / {_config.RustFsSecretKey}).",
            getEnabled: () => _config.EnableRustFs,
            setEnabled: v => _config.EnableRustFs = v,
            port1Label: "API:",
            getPort1: () => _config.RustFsApiPort,
            setPort1: p => { _config.RustFsApiPort = p; _rustfs.UpdatePort(p); },
            port2Label: "Console:",
            getPort2: () => _config.RustFsConsolePort,
            setPort2: p => _config.RustFsConsolePort = p,
            uiButtonPrefix: "Console",
            uiButtonIcon: IconKind.Globe,
            onOpenUi: () =>
            {
                try { Process.Start(new ProcessStartInfo($"http://127.0.0.1:{_config.RustFsConsolePort}") { UseShellExecute = true }); }
                catch { }
            },
            getEnvSnippet: BuildRustFsEnv,
            toolTip: serviceTip);

        // Reverse dock order for DockStyle.Top
        pageDevServices.Controls.AddRange([cardRustFs, cardMailpit]);
        cardMailpit.BringToFront();
        cardRustFs.BringToFront();
    }

    private Panel CreateDevServiceCard(
        IService service,
        string packageKey,
        string title,
        string description,
        Func<bool> getEnabled,
        Action<bool> setEnabled,
        string port1Label,
        Func<int> getPort1,
        Action<int> setPort1,
        string port2Label,
        Func<int> getPort2,
        Action<int> setPort2,
        string uiButtonPrefix,
        IconKind uiButtonIcon,
        Action onOpenUi,
        Func<string> getEnvSnippet,
        ToolTip toolTip,
        string? extraButtonText = null,
        string? extraButtonTooltip = null,
        Func<ModernButton, Task>? onExtraAction = null)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 104,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 6, 16, 16)
        };
        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = card.Height - 10f;
            var rect = new RectangleF(halfStroke, halfStroke, card.Width - stroke, cardH - stroke);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var bgBrush = new SolidBrush(ModernColors.Surface);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        var pill = new StatusPill
        {
            Status = service.Status,
            Location = new Point(14, 22)
        };
        service.StatusChanged += (s, st) =>
        {
            if (_isShuttingDown || IsDisposed || !IsHandleCreated || pill.IsDisposed) return;
            if (InvokeRequired) try { BeginInvoke(() => { if (!_isShuttingDown && !pill.IsDisposed) pill.Status = st; }); } catch { }
            else pill.Status = st;
        };

        var lblTitle = new Label
        {
            Text = title,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = new Point(116, 10),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = description,
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(117, 32),
            AutoSize = true
        };

        var lblEnable = new Label
        {
            Text = "Enabled:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(117, 62),
            AutoSize = true
        };
        var togEnable = new ModernToggle
        {
            Checked = getEnabled(),
            Location = new Point(170, 60)
        };
        togEnable.CheckedChanged += (s, e) =>
        {
            setEnabled(togEnable.Checked);
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            BuildServiceCards();
            BuildTrayMenu();
        };

        var lblPort1 = new Label
        {
            Text = port1Label,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(224, 62),
            AutoSize = true
        };
        var txtPort1 = CreatePortInput(getPort1(), p =>
        {
            setPort1(p);
            toolTip.SetToolTip(lblTitle, getEnvSnippet());
        });
        txtPort1.Location = new Point(lblPort1.Left + (port1Label.Length * 7) + 4, 59);

        var lblPort2 = new Label
        {
            Text = port2Label,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(txtPort1.Right + 10, 62),
            AutoSize = true
        };

        var btnOpenUi = new ModernButton
        {
            Text = $"{uiButtonPrefix} (:{getPort2()})",
            IconKind = uiButtonIcon,
            IconSize = 10,
            Width = 122,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnOpenUi.Click += (s, e) => onOpenUi();

        var txtPort2 = CreatePortInput(getPort2(), p =>
        {
            setPort2(p);
            btnOpenUi.Text = $"{uiButtonPrefix} (:{p})";
        });
        txtPort2.Location = new Point(lblPort2.Left + (port2Label.Length * 7) + 4, 59);

        var btnCopyEnv = new ModernButton
        {
            Text = "Copy .env",
            IconKind = IconKind.Code,
            IconSize = 10,
            Width = 84,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        toolTip.SetToolTip(btnCopyEnv, "Click to copy .env snippet:\r\n" + getEnvSnippet());
        btnCopyEnv.Click += async (s, e) =>
        {
            string env = getEnvSnippet();
            Clipboard.SetText(env);
            toolTip.SetToolTip(btnCopyEnv, "Click to copy .env snippet:\r\n" + env);
            btnCopyEnv.Text = "Copied!";
            await Task.Delay(1400);
            btnCopyEnv.Text = "Copy .env";
        };

        ModernButton? btnExtra = null;
        if (!string.IsNullOrEmpty(extraButtonText) && onExtraAction != null)
        {
            btnExtra = new ModernButton
            {
                Text = extraButtonText,
                IconKind = IconKind.Lightning,
                IconSize = 10,
                Width = 66,
                Height = 32,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.TextPrimary,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
            };
            if (!string.IsNullOrEmpty(extraButtonTooltip))
            {
                toolTip.SetToolTip(btnExtra, extraButtonTooltip);
            }
            btnExtra.Click += async (s, e) => await onExtraAction(btnExtra);
        }

        var btnToggle = new ModernButton
        {
            Text = service.Status == ServiceStatus.Running ? "Stop" : (service.Status == ServiceStatus.NotInstalled ? "Install" : "Start"),
            IconKind = service.Status == ServiceStatus.Running ? IconKind.Stop : (service.Status == ServiceStatus.NotInstalled ? IconKind.Download : IconKind.Play),
            IconSize = 10,
            Width = 80,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = service.Status == ServiceStatus.Running ? ModernColors.Danger : (service.Status == ServiceStatus.NotInstalled ? ModernColors.Primary : ModernColors.Success),
            HoverColor = service.Status == ServiceStatus.Running ? ModernColors.DangerHover : (service.Status == ServiceStatus.NotInstalled ? ModernColors.PrimaryHover : ModernColors.SuccessHover),
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        void UpdateToggleBtn()
        {
            if (service.Status == ServiceStatus.Running)
            {
                btnToggle.Text = "Stop";
                btnToggle.IconKind = IconKind.Stop;
                btnToggle.NormalColor = ModernColors.Danger;
                btnToggle.HoverColor = ModernColors.DangerHover;
            }
            else if (service.Status == ServiceStatus.NotInstalled)
            {
                btnToggle.Text = "Install";
                btnToggle.IconKind = IconKind.Download;
                btnToggle.NormalColor = ModernColors.Primary;
                btnToggle.HoverColor = ModernColors.PrimaryHover;
            }
            else
            {
                btnToggle.Text = "Start";
                btnToggle.IconKind = IconKind.Play;
                btnToggle.NormalColor = ModernColors.Success;
                btnToggle.HoverColor = ModernColors.SuccessHover;
            }
        }

        btnToggle.Click += async (s, e) =>
        {
            btnToggle.Enabled = false;
            try
            {
                if (service.Status == ServiceStatus.NotInstalled)
                {
                    await InstallServiceWithUiAsync(packageKey, service);
                }
                else if (service.Status == ServiceStatus.Running)
                {
                    await service.StopAsync();
                }
                else
                {
                    await service.StartAsync();
                }
            }
            finally
            {
                btnToggle.Enabled = true;
                UpdateToggleBtn();
            }
        };

        service.StatusChanged += (s, st) =>
        {
            if (_isShuttingDown || IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) try { BeginInvoke((Action)(() => { if (!_isShuttingDown && !IsDisposed) UpdateToggleBtn(); })); } catch { }
            else UpdateToggleBtn();
        };

        void LayoutButtons()
        {
            const int y = 14;
            int rightX = card.Width > 200 ? card.Width - btnToggle.Width - 14 : 520;
            btnToggle.Location = new Point(rightX, y);
            btnOpenUi.Location = new Point(btnToggle.Left - btnOpenUi.Width - 8, y);
            btnCopyEnv.Location = new Point(btnOpenUi.Left - btnCopyEnv.Width - 8, y);

            int leftLimit = btnCopyEnv.Left - 10;
            if (btnExtra != null)
            {
                btnExtra.Location = new Point(btnCopyEnv.Left - btnExtra.Width - 8, y);
                leftLimit = btnExtra.Left - 10;
            }

            int maxDescW = leftLimit - lblDesc.Left;
            if (maxDescW > 50)
            {
                lblTitle.MaximumSize = new Size(maxDescW, 22);
                lblTitle.AutoEllipsis = true;
                lblDesc.MaximumSize = new Size(maxDescW, 20);
                lblDesc.AutoEllipsis = true;
            }
        }

        card.Resize += (s, e) => LayoutButtons();
        LayoutButtons();

        card.Controls.AddRange([
            pill, lblTitle, lblDesc,
            lblEnable, togEnable,
            lblPort1, txtPort1,
            lblPort2, txtPort2,
            btnCopyEnv, btnOpenUi, btnToggle
        ]);
        if (btnExtra != null) card.Controls.Add(btnExtra);

        return card;
    }

    // ==========================================
    // UI REUSABLE CARD HELPERS
    // ==========================================
    private ModernTextBox CreatePortInput(int currentPort, Action<int> onPortSaved)
    {
        var txt = new ModernTextBox
        {
            Text = currentPort.ToString(),
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.Primary,
            BorderRadius = 6,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            Width = 60,
            Height = 26
        };

        void SavePort()
        {
            if (int.TryParse(txt.Text.Trim(), out int port) && port > 0 && port <= 65535)
            {
                if (port != currentPort)
                {
                    currentPort = port;
                    onPortSaved(port);
                    ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                }
            }
            else
            {
                txt.Text = currentPort.ToString();
            }
        }

        txt.Leave += (s, e) => SavePort();
        txt.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                SavePort();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        return txt;
    }

    private static Panel CreateSubSectionHeader(string title)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(0, 6, 0, 4)
        };
        var lblT = new Label
        {
            Text = title,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(0, 4),
            AutoSize = true
        };
        pnl.Controls.Add(lblT);
        return pnl;
    }

    private static Panel CreateSettingsSectionHeader(string title, bool isFirst = false)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Top,
            Height = isFirst ? 28 : 42,
            Padding = new Padding(0, isFirst ? 0 : 14, 0, 4)
        };
        var lblT = new Label
        {
            Text = title,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
            Location = new Point(0, isFirst ? 0 : 14),
            AutoSize = true
        };
        pnl.Controls.Add(lblT);
        return pnl;
    }

    private static Panel CreateOptionCard(string title, string desc, Control rightControl)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 6, 16, 14)
        };
        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = card.Height - 8f;
            var rect = new RectangleF(halfStroke, halfStroke, card.Width - stroke, cardH - stroke);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bgBrush = new SolidBrush(ModernColors.Surface);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);
        };

        var lblT = new Label
        {
            Text = title,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
            Location = new Point(14, 8),
            AutoSize = true
        };

        var lblD = new Label
        {
            Text = desc,
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8f),
            Location = new Point(15, 29),
            AutoSize = true
        };

        void LayoutRightControl()
        {
            int cardH = card.Height - 8;
            int rightX = card.Width > 150 ? card.Width - rightControl.Width - 16 : 450;
            rightControl.Location = new Point(rightX, (cardH - rightControl.Height) / 2);

            int maxDescW = rightControl.Left - lblD.Left - 10;
            if (maxDescW > 50)
            {
                lblT.MaximumSize = new Size(maxDescW, 20);
                lblT.AutoEllipsis = true;
                lblD.MaximumSize = new Size(maxDescW, 20);
                lblD.AutoEllipsis = true;
            }
        }
        card.Resize += (s, e) => LayoutRightControl();
        LayoutRightControl();

        card.Controls.Add(lblT);
        card.Controls.Add(lblD);
        card.Controls.Add(rightControl);
        return card;
    }

    // ------------------------------------------
    // PAGE 6: BACKGROUND WORKERS (WINDOWS SUPERVISOR)
    // ------------------------------------------
    private void PopulateWorkersPage()
    {
        pageWorkers.Padding = new Padding(24, 16, 24, 20);

        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(0, 0, 0, 8)
        };

        var btnAddWorker = new ModernButton
        {
            Text = "Add Worker",
            IconKind = IconKind.Plus,
            IconSize = 10,
            Width = 108,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(0, 2)
        };
        btnAddWorker.Click += (s, e) =>
        {
            using var dlg = new WorkerDialog(_appRoot);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultWorker != null)
            {
                WorkerManager.AddWorker(_appRoot, dlg.ResultWorker);
                RefreshWorkersList();
                lblStatusText.Text = $"Worker '{dlg.ResultWorker.Name}' added.";
                lblStatusText.ForeColor = ModernColors.Success;
            }
        };

        var btnStartAuto = new ModernButton
        {
            Text = "Start Auto",
            IconKind = IconKind.Play,
            IconSize = 10,
            Width = 100,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(116, 2)
        };
        btnStartAuto.Click += (s, e) =>
        {
            WorkerManager.StartAllAutoWorkers(_appRoot, _job);
        SchedulerManager.StartAutoTasks(_appRoot, _job);
            RefreshWorkersList();
        };

        var btnStopAllWorkers = new ModernButton
        {
            Text = "Stop All",
            IconKind = IconKind.Stop,
            IconSize = 10,
            Width = 90,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.DangerBg,
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(224, 2)
        };
        btnStopAllWorkers.Click += (s, e) =>
        {
            WorkerManager.StopAllWorkers();
        SchedulerManager.StopAll();
            RefreshWorkersList();
        };

        var btnRefresh = new ModernButton
        {
            Text = "Refresh",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 84,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(322, 2)
        };
        btnRefresh.Click += (s, e) => RefreshWorkersList();

        var lblDesc = new Label
        {
            Text = "Windows Supervisor: manage long-running dev servers (npm run dev), queue listeners, and schedulers cleanly via JobObject.",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(0, 42),
            AutoSize = true
        };

        pnlTop.Controls.AddRange([btnAddWorker, btnStartAuto, btnStopAllWorkers, btnRefresh, lblDesc]);

        _pnlWorkersContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 12)
        };
        _pnlWorkersContainer.Resize += (s, e) =>
        {
            int targetW = _pnlWorkersContainer.ClientSize.Width - 18;
            if (targetW < 300) return;
            foreach (Control c in _pnlWorkersContainer.Controls)
            {
                c.Width = targetW;
            }
        };

        var pnlWorkersView = new Panel { Dock = DockStyle.Fill };
        pnlWorkersView.Controls.Add(_pnlWorkersContainer);
        pnlWorkersView.Controls.Add(pnlTop);

        // Scheduler Sub-View
        var pnlSchedulerView = new Panel { Dock = DockStyle.Fill, Visible = false };
        var pnlSchedTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(0, 0, 0, 8)
        };

        var btnAddSched = new ModernButton
        {
            Text = "Add Schedule",
            IconKind = IconKind.Plus,
            IconSize = 10,
            Width = 118,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(0, 2)
        };
        btnAddSched.Click += (s, e) =>
        {
            using var dlg = new SchedulerDialog(_appRoot);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultTask != null)
            {
                SchedulerManager.AddTask(_appRoot, dlg.ResultTask);
                RefreshSchedulersList();
                lblStatusText.Text = $"Schedule '{dlg.ResultTask.Name}' added.";
                lblStatusText.ForeColor = ModernColors.Success;
            }
        };

        var btnStartAutoSched = new ModernButton
        {
            Text = "Start Auto",
            IconKind = IconKind.Play,
            IconSize = 10,
            Width = 100,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(126, 2)
        };
        btnStartAutoSched.Click += (s, e) =>
        {
            SchedulerManager.StartAutoTasks(_appRoot, _job);
            RefreshSchedulersList();
        };

        var btnStopAllSched = new ModernButton
        {
            Text = "Stop All",
            IconKind = IconKind.Stop,
            IconSize = 10,
            Width = 90,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.DangerBg,
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(234, 2)
        };
        btnStopAllSched.Click += (s, e) =>
        {
            SchedulerManager.StopAll();
            RefreshSchedulersList();
        };

        var btnRefreshSched = new ModernButton
        {
            Text = "Refresh",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 86,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(332, 2)
        };
        btnRefreshSched.Click += (s, e) => RefreshSchedulersList();

        var lblSchedDesc = new Label
        {
            Text = "Run periodic one-shot commands (e.g. php artisan schedule:run every 60s) without Windows Task Scheduler.",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(0, 44),
            AutoSize = true
        };
        pnlSchedTop.Controls.AddRange([btnAddSched, btnStartAutoSched, btnStopAllSched, btnRefreshSched, lblSchedDesc]);

        _pnlSchedulersContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 12)
        };
        _pnlSchedulersContainer.Resize += (s, e) =>
        {
            int targetW = _pnlSchedulersContainer.ClientSize.Width - 18;
            if (targetW < 300) return;
            foreach (Control c in _pnlSchedulersContainer.Controls)
            {
                c.Width = targetW;
            }
        };

        pnlSchedulerView.Controls.Add(_pnlSchedulersContainer);
        pnlSchedulerView.Controls.Add(pnlSchedTop);

        // Segmented Sub-Tab Bar
        var pnlSubTabs = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(0, 0, 0, 8)
        };

        var btnTabWorkers = new ModernButton
        {
            Text = "Background Workers",
            Width = 148,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(0, 0)
        };
        var btnTabScheduler = new ModernButton
        {
            Text = "Scheduler",
            Width = 104,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(156, 0)
        };

        void SelectWorkersSubTab(bool showScheduler)
        {
            pnlWorkersView.Visible = !showScheduler;
            pnlSchedulerView.Visible = showScheduler;

            btnTabWorkers.NormalColor = !showScheduler ? ModernColors.Primary : ModernColors.Card;
            btnTabWorkers.HoverColor = !showScheduler ? ModernColors.PrimaryHover : ModernColors.SurfaceHover;
            btnTabWorkers.ForeColor = !showScheduler ? Color.White : ModernColors.TextSecondary;
            btnTabWorkers.ShowBorder = showScheduler;

            btnTabScheduler.NormalColor = showScheduler ? ModernColors.Primary : ModernColors.Card;
            btnTabScheduler.HoverColor = showScheduler ? ModernColors.PrimaryHover : ModernColors.SurfaceHover;
            btnTabScheduler.ForeColor = showScheduler ? Color.White : ModernColors.TextSecondary;
            btnTabScheduler.ShowBorder = !showScheduler;

            if (showScheduler) RefreshSchedulersList();
            else RefreshWorkersList();
        }

        btnTabWorkers.Click += (s, e) => SelectWorkersSubTab(false);
        btnTabScheduler.Click += (s, e) => SelectWorkersSubTab(true);
        pnlSubTabs.Controls.AddRange([btnTabWorkers, btnTabScheduler]);

        SchedulerManager.TaskUpdated += (s, t) =>
        {
            if (pageWorkers.Visible && pnlSchedulerView.Visible && !IsDisposed)
            {
                BeginInvoke(RefreshSchedulersList);
            }
        };

        pageWorkers.Controls.Add(pnlWorkersView);
        pageWorkers.Controls.Add(pnlSchedulerView);
        pageWorkers.Controls.Add(pnlSubTabs);

        RefreshWorkersList();
    }

    private FlowLayoutPanel? _pnlSchedulersContainer;

    private void RefreshSchedulersList()
    {
        if (_pnlSchedulersContainer == null || _pnlSchedulersContainer.IsDisposed) return;

        _pnlSchedulersContainer.SuspendLayout();
        _pnlSchedulersContainer.Controls.Clear();

        var tasks = SchedulerManager.Tasks;
        int targetW = Math.Max(300, _pnlSchedulersContainer.ClientSize.Width - 18);

        if (tasks.Count == 0)
        {
            var empty = new Label
            {
                Text = "No scheduled tasks configured yet.\nClick '+ Add Schedule' to run php artisan schedule:run periodically.",
                ForeColor = ModernColors.TextMuted,
                Font = new Font("Segoe UI", 9.5f),
                AutoSize = true,
                Padding = new Padding(4, 16, 4, 16)
            };
            _pnlSchedulersContainer.Controls.Add(empty);
        }
        else
        {
            foreach (var task in tasks)
            {
                _pnlSchedulersContainer.Controls.Add(CreateSchedulerCard(task, targetW));
            }
        }

        _pnlSchedulersContainer.ResumeLayout();
    }

    private Panel CreateSchedulerCard(SchedulerItem task, int width)
    {
        var card = new Panel
        {
            Width = width,
            Height = 92,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.Transparent
        };
        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, card.Width - 1f, card.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var bg = new SolidBrush(ModernColors.Surface);
            g.FillPath(bg, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        string statusText = task.IsRunning ? "Running" : (task.IsActive ? "Active" : "Paused");
        Color statusColor = task.IsRunning ? ModernColors.Primary : (task.IsActive ? ModernColors.Success : ModernColors.TextMuted);

        var lblTitle = new Label
        {
            Text = task.Name,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(16, 12),
            AutoSize = true
        };

        var lblStatus = new Label
        {
            Text = $"● {statusText}",
            ForeColor = statusColor,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(16, 36),
            AutoSize = true
        };

        string lastRun = task.LastRunAt.HasValue
            ? $"Last: {task.LastRunAt.Value:HH:mm:ss} (Exit {task.LastExitCode ?? 0}, {task.LastDurationMs}ms)"
            : "Last: Never";
        var lblMeta = new Label
        {
            Text = $"{task.Command}  •  Every {task.IntervalSeconds}s  •  {lastRun}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(16, 58),
            AutoSize = true
        };

        var btnDelete = new ModernButton
        {
            Text = "Delete",
            Width = 62,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.DangerBg,
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnDelete.Click += (s, e) =>
        {
            if (MessageBox.Show(this, $"Delete schedule '{task.Name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                SchedulerManager.DeleteTask(_appRoot, task.Id);
                RefreshSchedulersList();
            }
        };

        var btnEdit = new ModernButton
        {
            Text = "Edit",
            Width = 54,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnEdit.Click += (s, e) =>
        {
            using var dlg = new SchedulerDialog(_appRoot, task);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultTask != null)
            {
                SchedulerManager.UpdateTask(_appRoot, dlg.ResultTask);
                RefreshSchedulersList();
            }
        };

        var btnLogs = new ModernButton
        {
            Text = "Logs",
            Width = 56,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnLogs.Click += (s, e) =>
        {
            string logs = task.GetLogsText();
            MessageBox.Show(this, string.IsNullOrWhiteSpace(logs) ? "(No logs yet)" : logs, $"Schedule Logs: {task.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnRunNow = new ModernButton
        {
            Text = task.IsRunning ? "Running..." : "Run Now",
            Enabled = !task.IsRunning,
            Width = 76,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnRunNow.Click += async (s, e) => await SchedulerManager.RunNowAsync(_appRoot, _job, task);

        var btnToggle = new ModernButton
        {
            Text = task.IsActive ? "Pause" : "Start",
            IconKind = task.IsActive ? IconKind.Stop : IconKind.Play,
            IconSize = 9,
            Width = 72,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = task.IsActive ? ModernColors.Danger : ModernColors.Success,
            HoverColor = task.IsActive ? ModernColors.DangerHover : ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnToggle.Click += (s, e) =>
        {
            if (task.IsActive) SchedulerManager.StopTask(task);
            else SchedulerManager.StartTask(_appRoot, _job, task);
            RefreshSchedulersList();
        };

        void LayoutButtons()
        {
            int y = 32;
            btnDelete.Location = new Point(card.Width - btnDelete.Width - 14, y);
            btnEdit.Location = new Point(btnDelete.Left - btnEdit.Width - 6, y);
            btnLogs.Location = new Point(btnEdit.Left - btnLogs.Width - 6, y);
            btnRunNow.Location = new Point(btnLogs.Left - btnRunNow.Width - 6, y);
            btnToggle.Location = new Point(btnRunNow.Left - btnToggle.Width - 6, y);

            int maxW = btnToggle.Left - 24;
            if (maxW > 80)
            {
                lblMeta.MaximumSize = new Size(maxW, 20);
                lblMeta.AutoEllipsis = true;
            }
        }
        card.Resize += (s, e) => LayoutButtons();
        LayoutButtons();

        card.Controls.AddRange([lblTitle, lblStatus, lblMeta, btnToggle, btnRunNow, btnLogs, btnEdit, btnDelete]);
        return card;
    }

    private void RefreshWorkersList()
    {
        if (_pnlWorkersContainer == null || _pnlWorkersContainer.IsDisposed) return;

        _pnlWorkersContainer.SuspendLayout();
        _pnlWorkersContainer.Controls.Clear();

        var workers = WorkerManager.Workers;
        int targetW = Math.Max(300, _pnlWorkersContainer.ClientSize.Width - 18);

        if (workers.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "No background workers configured yet.\nClick '+ Add Worker' to add npm run dev, php artisan queue:listen, or custom scripts.",
                ForeColor = ModernColors.TextMuted,
                Font = new Font("Segoe UI", 9.25f),
                AutoSize = true,
                Padding = new Padding(8, 20, 8, 20)
            };
            _pnlWorkersContainer.Controls.Add(lblEmpty);
        }
        else
        {
            foreach (var worker in workers)
            {
                var card = CreateWorkerCard(worker);
                card.Width = targetW;
                _pnlWorkersContainer.Controls.Add(card);
            }
        }

        _pnlWorkersContainer.ResumeLayout();
    }

    private Panel CreateWorkerCard(WorkerItem worker)
    {
        var card = new Panel
        {
            Height = 78,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.Transparent,
            Padding = new Padding(14, 10, 14, 10)
        };

        Color cardBg = ModernColors.Surface;
        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, card.Width - 1f, card.Height - 5f);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var bgBrush = new SolidBrush(cardBg);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        var pillStatus = new StatusPill
        {
            Text = worker.Status,
            Status = worker.IsRunning ? ServiceStatus.Running : (worker.Status == "Failed" ? ServiceStatus.Error : ServiceStatus.Stopped),
            Location = new Point(14, 15),
            Size = new Size(82, 22)
        };

        var lblName = new Label
        {
            Text = worker.Name,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(106, 12),
            AutoSize = true
        };

        string subText = $"{worker.Command}  •  {worker.WorkingDirectory}";
        if (!string.IsNullOrEmpty(worker.SiteDomain))
        {
            subText += $"  [{worker.SiteDomain}]";
        }
        if (worker.AutoStart)
        {
            subText += "  (Auto)";
        }

        var lblSub = new Label
        {
            Text = subText,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f),
            Location = new Point(107, 36),
            AutoSize = true,
            AutoEllipsis = true
        };

        var tip = new ToolTip();

        var btnToggle = new ModernButton
        {
            Text = worker.IsRunning ? "Stop" : "Start",
            IconKind = worker.IsRunning ? IconKind.Stop : IconKind.Play,
            IconSize = 10,
            Width = 74,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = false,
            NormalColor = worker.IsRunning ? ModernColors.Danger : ModernColors.Success,
            HoverColor = worker.IsRunning ? ModernColors.DangerHover : ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };

        var btnLogs = new ModernButton
        {
            Text = "Logs",
            IconKind = IconKind.Terminal,
            IconSize = 10,
            Width = 68,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = cardBg,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        tip.SetToolTip(btnLogs, "Open live terminal output logs");

        var btnEdit = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Gear,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            CustomSurfaceColor = cardBg,
            ForeColor = ModernColors.TextSecondary
        };
        tip.SetToolTip(btnEdit, "Edit worker configuration");

        var btnDelete = new ModernButton
        {
            Text = "",
            IconKind = IconKind.Trash,
            IconSize = 12,
            Width = 28,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.DangerBg,
            CustomSurfaceColor = cardBg,
            ForeColor = ModernColors.Danger
        };
        tip.SetToolTip(btnDelete, "Delete worker");

        void SyncWorkerUi()
        {
            if (card.IsDisposed) return;
            bool running = worker.IsRunning;
            pillStatus.Text = worker.Status;
            pillStatus.Status = running ? ServiceStatus.Running : (worker.Status == "Failed" ? ServiceStatus.Error : ServiceStatus.Stopped);
            btnToggle.Text = running ? "Stop" : "Start";
            btnToggle.IconKind = running ? IconKind.Stop : IconKind.Play;
            btnToggle.NormalColor = running ? ModernColors.Danger : ModernColors.Success;
            btnToggle.HoverColor = running ? ModernColors.DangerHover : ModernColors.SuccessHover;
        }

        Action<string> statusHandler = _ =>
        {
            if (_isShuttingDown || card.IsDisposed || !card.IsHandleCreated) return;
            try { card.BeginInvoke(SyncWorkerUi); } catch { }
        };
        worker.StatusChanged += statusHandler;
        card.Disposed += (_, _) => worker.StatusChanged -= statusHandler;

        btnToggle.Click += (s, e) =>
        {
            if (worker.IsRunning)
            {
                WorkerManager.StopWorker(worker);
            }
            else
            {
                WorkerManager.StartWorker(worker, _appRoot, _job);
            }
            SyncWorkerUi();
        };

        btnLogs.Click += (s, e) =>
        {
            if (_openWorkerLogs.TryGetValue(worker.Id, out var existing) && !existing.IsDisposed)
            {
                if (existing.WindowState == FormWindowState.Minimized)
                    existing.WindowState = FormWindowState.Normal;
                existing.BringToFront();
                existing.Focus();
                return;
            }

            var dlg = new WorkerLogsDialog(worker);
            _openWorkerLogs[worker.Id] = dlg;
            dlg.FormClosed += (_, _) => _openWorkerLogs.Remove(worker.Id);
            dlg.Show(this);
        };

        btnEdit.Click += (s, e) =>
        {
            using var dlg = new WorkerDialog(_appRoot, worker);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultWorker != null)
            {
                WorkerManager.UpdateWorker(_appRoot, dlg.ResultWorker);
                RefreshWorkersList();
            }
        };

        btnDelete.Click += (s, e) =>
        {
            var res = MessageBox.Show(this, $"Delete worker '{worker.Name}'?", "Delete Worker", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                WorkerManager.RemoveWorker(_appRoot, worker.Id);
                RefreshWorkersList();
            }
        };

        void LayoutWorkerCard()
        {
            int cardH = card.Height - 5;
            int rightX = card.Width > 200 ? card.Width - btnDelete.Width - 14 : 500;
            btnDelete.Location = new Point(rightX, (cardH - btnDelete.Height) / 2);
            btnEdit.Location = new Point(btnDelete.Left - btnEdit.Width - 6, (cardH - btnEdit.Height) / 2);
            btnLogs.Location = new Point(btnEdit.Left - btnLogs.Width - 6, (cardH - btnLogs.Height) / 2);
            btnToggle.Location = new Point(btnLogs.Left - btnToggle.Width - 6, (cardH - btnToggle.Height) / 2);

            int maxTextW = Math.Max(60, btnToggle.Left - lblName.Left - 12);
            lblName.MaximumSize = new Size(maxTextW, 22);
            lblSub.MaximumSize = new Size(maxTextW, 20);
        }
        card.Resize += (s, e) => LayoutWorkerCard();
        LayoutWorkerCard();

        card.Controls.AddRange([pillStatus, lblName, lblSub, btnToggle, btnLogs, btnEdit, btnDelete]);
        return card;
    }

    // ==========================================
    // LIFECYCLE CONTROLLERS
    // ==========================================
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

    private readonly ToolTip _footerErrorTip = new() { InitialDelay = 200, AutoPopDelay = 12000 };

    private void InitFooterErrorCopy()
    {
        lblStatusText.TextChanged += (s, e) => UpdateFooterErrorCopyButton();
        lblStatusText.ForeColorChanged += (s, e) => UpdateFooterErrorCopyButton();

        btnCopyFooterError.Click += async (s, e) =>
        {
            string text = lblStatusText.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                Clipboard.SetText(text);
                btnCopyFooterError.Text = "Copied!";
                await Task.Delay(1400);
                btnCopyFooterError.Text = "Copy Error";
            }
            catch { }
        };

        UpdateFooterErrorCopyButton();
    }

    private void UpdateFooterErrorCopyButton()
    {
        bool isError = (lblStatusText.ForeColor == ModernColors.Danger || lblStatusText.ForeColor == ModernColors.Warning)
                       && !string.IsNullOrWhiteSpace(lblStatusText.Text);
        btnCopyFooterError.Visible = isError;
        btnCopyFooterError.ForeColor = lblStatusText.ForeColor == ModernColors.Warning ? ModernColors.Warning : ModernColors.Danger;

        string tip = isError ? lblStatusText.Text : string.Empty;
        _footerErrorTip.SetToolTip(lblStatusText, tip);
        _footerErrorTip.SetToolTip(btnCopyFooterError, isError ? $"Copy error message:\r\n{tip}" : string.Empty);

        LayoutFooterControls();
    }

    private void LayoutFooterControls()
    {
        lblStatsText.Location = new Point(footerPanel.ClientSize.Width - lblStatsText.Width - 20, 7);

        int rightBoundary = lblStatsText.Left;
        if (btnCopyFooterError.Visible)
        {
            btnCopyFooterError.Location = new Point(lblStatsText.Left - btnCopyFooterError.Width - 10, 5);
            rightBoundary = btnCopyFooterError.Left;
        }

        int maxStatusW = rightBoundary - lblStatusText.Left - 10;
        if (maxStatusW > 50)
        {
            lblStatusText.MaximumSize = new Size(maxStatusW, 18);
            lblStatusText.AutoEllipsis = true;
        }
    }

    private void UpdateServiceStats()
    {
        if (_isShuttingDown || IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(UpdateServiceStats); } catch { }
            return;
        }

        int enabledCount = _services.Count(s => s != _php && _config.IsServiceEnabled(s.Name));
        int runningCount = _services.Count(s => s != _php && _config.IsServiceEnabled(s.Name) && s.Status == ServiceStatus.Running);
        bool anyError = _services.Any(s => s != _php && _config.IsServiceEnabled(s.Name) && s.Status == ServiceStatus.Error);

        lblStatsText.Text = $"Services: {runningCount} / {enabledCount} running";
        lblStatsText.ForeColor = runningCount > 0 ? ModernColors.Success : ModernColors.TextMuted;
        LayoutFooterControls();

        _sidebarDotColor = anyError ? ModernColors.Danger : (runningCount > 0 ? ModernColors.Success : ModernColors.TextMuted);
        lblSidebarStatus.Text = anyError ? "Service Error" : (runningCount > 0 ? $"{runningCount} running" : "Stopped");
        sidebarFooter.Invalidate();

        // Dynamic Smart Master Hero Button
        if (runningCount > 0)
        {
            btnStartAll.Text = "Stop All";
            btnStartAll.IconKind = IconKind.Stop;
            btnStartAll.NormalColor = ModernColors.Card;
            btnStartAll.HoverColor = ModernColors.DangerBg;
            btnStartAll.PressedColor = ModernColors.DangerBg;
            btnStartAll.ForeColor = ModernColors.Danger;
            btnStartAll.ShowBorder = true;
            btnStartAll.BorderLineColor = Color.FromArgb(120, ModernColors.Danger);
        }
        else
        {
            btnStartAll.Text = "Start All";
            btnStartAll.IconKind = IconKind.Play;
            btnStartAll.NormalColor = ModernColors.Success;
            btnStartAll.HoverColor = ModernColors.SuccessHover;
            btnStartAll.PressedColor = ModernColors.SuccessBg;
            btnStartAll.ForeColor = Color.White;
            btnStartAll.ShowBorder = false;
        }
        btnStartAll.Invalidate();
    }

    private async Task InstallServiceWithUiAsync(string serviceKey, IService targetService)
    {
        var pkg = PackageCatalog.GetServicePackage(serviceKey);
        if (pkg == null)
        {
            MessageBox.Show(this, $"No download package found for {targetService.Name}.", "Install Service", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Download and install {pkg.Name} ({pkg.Tag})?\nApproximate download size: ~{pkg.ApproximateSizeBytes / (1024 * 1024)} MB",
            $"Install {targetService.Name}",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        lblStatusText.Text = $"Connecting to download {pkg.Name}...";
        lblStatusText.ForeColor = ModernColors.Primary;

        var progress = new Progress<PackageDownloadProgress>(p =>
        {
            if (p.HasError)
            {
                lblStatusText.Text = $"Installation error: {p.ErrorMessage}";
                lblStatusText.ForeColor = ModernColors.Danger;
            }
            else
            {
                lblStatusText.Text = p.StatusText;
                lblStatusText.ForeColor = ModernColors.Primary;
            }
        });

        bool success = await PackageDownloader.DownloadAndInstallAsync(pkg, _appRoot, progress);
        if (success)
        {
            targetService.CheckInstallation();
            lblStatusText.Text = $"{pkg.Name} installed successfully and ready to start.";
            lblStatusText.ForeColor = ModernColors.Success;
            BuildServiceCards();
            BuildTrayMenu();
        }
        else
        {
            lblStatusText.Text = $"Failed to install {pkg.Name}.";
            lblStatusText.ForeColor = ModernColors.Danger;
        }
    }

    private async Task SwitchActivePhpAsync(string newVersion)
    {
        if (string.IsNullOrWhiteSpace(newVersion)) return;

        _config.ActivePhp = newVersion;
        ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        PhpService.UpdateCurrentJunction(_appRoot, newVersion);
        VirtualHostManager.GenerateVhostsConfig(_appRoot, _config, Path.Combine(_nginx.GetNginxDirectory(), "conf"));

        if (_php.Status == ServiceStatus.Running)
        {
            lblStatusText.Text = $"Switching PHP to {newVersion}...";
            await _php.RestartAsync();

            if (_nginx.Status == ServiceStatus.Running)
            {
                bool reloaded = await _nginx.ReloadAsync();
                if (!reloaded)
                {
                    await _nginx.RestartAsync();
                }
            }
            lblStatusText.Text = $"Switched to PHP {newVersion}.";
        }

        RefreshPhpVersionsCombo();
        RefreshSitesList();
        _refreshPhpPackages?.Invoke();
        BuildServiceCards();
        BuildTrayMenu();
        _refreshSqlServerCard?.Invoke();
    }

    private void UpdatePhpBannerText()
    {
        if (_lblPhpTitle != null)
        {
            _lblPhpTitle.Text = $"PHP {_config.ActivePhp} (Active)";
        }
        if (_lblPhpSub != null)
        {
            _lblPhpSub.Text = "Supervisor Pool: 5000 reqs  •  Laravel Stack Ready";
        }
        if (_pillPhp != null)
        {
            _pillPhp.Status = _php.Status;
        }
    }

    private void RefreshPhpVersionsCombo()
    {
        UpdatePhpBannerText();
        if (_cmbPhpVersions == null) return;
        _cmbPhpVersions.Items.Clear();
        var versions = PhpService.GetInstalledVersions(_appRoot);
        if (versions.Count > 0) _cmbPhpVersions.Items.AddRange(versions.ToArray());
        if (_cmbPhpVersions.Items.Count == 0) _cmbPhpVersions.Items.Add(_config.ActivePhp);
        if (_cmbPhpVersions.Items.Contains(_config.ActivePhp))
            _cmbPhpVersions.SelectedItem = _config.ActivePhp;
        else if (_cmbPhpVersions.Items.Count > 0)
            _cmbPhpVersions.SelectedIndex = 0;
    }

    private void BuildServiceCards()
    {
        cardContainer.Controls.Clear();

        var phpVersions = PhpService.GetInstalledVersions(_appRoot);
        if (phpVersions.Count == 0)
        {
            phpVersions.Add(_config.ActivePhp);
        }

        // Add enabled service cards in reverse dock order
        if (_config.EnableRustFs)
        {
            var rustfsCard = new ServiceCard(_rustfs, IconKind.Database, onInstall: async svc => await InstallServiceWithUiAsync("rustfs", svc));
            cardContainer.Controls.Add(rustfsCard);
        }

        if (_config.EnableRedis)
        {
            var redisCard = new ServiceCard(_redis, IconKind.Server, onInstall: async svc => await InstallServiceWithUiAsync("redis", svc));
            cardContainer.Controls.Add(redisCard);
        }

        if (_config.EnablePostgresql)
        {
            var pgCard = new ServiceCard(_postgresql, IconKind.Database, onInstall: async svc => await InstallServiceWithUiAsync("postgresql", svc));
            cardContainer.Controls.Add(pgCard);
        }

        if (_config.EnableMailpit)
        {
            var mailpitCard = new ServiceCard(_mailpit, IconKind.Mail, onInstall: async svc => await InstallServiceWithUiAsync("mailpit", svc));
            cardContainer.Controls.Add(mailpitCard);
        }

        if (_config.EnableMysql)
        {
            var mysqlCard = new ServiceCard(_mysql, IconKind.Database, onInstall: async svc => await InstallServiceWithUiAsync("mysql", svc));
            cardContainer.Controls.Add(mysqlCard);
        }

        if (_config.EnableNginx)
        {
            var nginxCard = new ServiceCard(
                _nginx,
                IconKind.Server,
                phpVersions.ToArray(),
                _config.ActivePhp,
                async newVersion => await SwitchActivePhpAsync(newVersion),
                onInstall: async svc => await InstallServiceWithUiAsync("nginx", svc)
            );
            cardContainer.Controls.Add(nginxCard);
        }

        if (cardContainer.Controls.Count == 0)
        {
            var pnlEmpty = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernColors.Background
            };
            var lblEmptyTitle = new Label
            {
                Text = "No services are currently enabled.",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ModernColors.TextPrimary,
                Location = new Point(20, 40),
                AutoSize = true
            };
            var lblEmptySub = new Label
            {
                Text = "Use the 'Services' tab or Settings to enable Nginx, MySQL, Mailpit, etc.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ModernColors.TextSecondary,
                Location = new Point(20, 68),
                AutoSize = true
            };
            var btnGoSettings = new ModernButton
            {
                Text = "Open Settings",
                IconKind = IconKind.Gear,
                IconSize = 11,
                Width = 140,
                Height = 32,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.Primary,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Location = new Point(20, 100)
            };
            btnGoSettings.Click += (s, e) => SelectNavTab(6);

            pnlEmpty.Controls.Add(lblEmptyTitle);
            pnlEmpty.Controls.Add(lblEmptySub);
            pnlEmpty.Controls.Add(btnGoSettings);
            cardContainer.Controls.Add(pnlEmpty);
        }

        UpdateServiceStats();
    }

    private void BuildTrayMenu()
    {
        trayMenu.Items.Clear();

        trayMenu.Items.Add(new ToolStripMenuItem("Dashboard", null, (s, e) => RestoreFromTray())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        });
        trayMenu.Items.Add(new ToolStripSeparator());

        // PHP Version submenu
        var phpSubMenu = new ToolStripMenuItem("PHP Version");
        var installedPhps = PhpService.GetInstalledVersions(_appRoot);
        foreach (var dirName in installedPhps)
        {
            var item = new ToolStripMenuItem(dirName, null, async (s, e) =>
            {
                await SwitchActivePhpAsync(dirName);
            })
            {
                Checked = dirName.Equals(_config.ActivePhp, StringComparison.OrdinalIgnoreCase)
            };
            phpSubMenu.DropDownItems.Add(item);
        }
        trayMenu.Items.Add(phpSubMenu);

        // Node Version submenu
        var nodeSubMenu = new ToolStripMenuItem("Node Version");
        var (sysNodeFound, sysNodePath, sysNodeVer) = NodeManager.DetectSystemNode();
        if (sysNodeFound)
        {
            var sysItem = new ToolStripMenuItem($"System Node ({sysNodeVer})", null, (s, e) =>
            {
                _config.ActiveNode = "system";
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                NodeManager.UpdateCurrentJunction(_appRoot, "system");
                if (_lblNodeTitle != null) _lblNodeTitle.Text = $"Active: System Node ({sysNodeVer})";
                _refreshNodePackages?.Invoke();
                BuildTrayMenu();
                lblStatusText.Text = $"System Node.js ({sysNodeVer}) active.";
            })
            {
                Checked = _config.ActiveNode.Equals("system", StringComparison.OrdinalIgnoreCase)
            };
            nodeSubMenu.DropDownItems.Add(sysItem);
        }

        var nvmVersions = NodeManager.DetectNvmVersions();
        foreach (var (nvmVer, nvmPath) in nvmVersions)
        {
            if (sysNodeFound && string.Equals(sysNodePath, nvmPath, StringComparison.OrdinalIgnoreCase)) continue;
            string key = $"system:{nvmPath}";
            var nvmItem = new ToolStripMenuItem($"Windows Node ({nvmVer})", null, (s, e) =>
            {
                _config.ActiveNode = key;
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                NodeManager.UpdateCurrentJunction(_appRoot, key);
                if (_lblNodeTitle != null) _lblNodeTitle.Text = $"Active: Windows Node ({nvmVer})";
                _refreshNodePackages?.Invoke();
                BuildTrayMenu();
                lblStatusText.Text = $"Windows Node.js ({nvmVer}) active.";
            })
            {
                Checked = string.Equals(_config.ActiveNode, key, StringComparison.OrdinalIgnoreCase)
            };
            nodeSubMenu.DropDownItems.Add(nvmItem);
        }

        var installedNodeVersions = NodeManager.GetInstalledVersions(_appRoot);
        if (nodeSubMenu.DropDownItems.Count > 0 && installedNodeVersions.Count > 0)
        {
            nodeSubMenu.DropDownItems.Add(new ToolStripSeparator());
        }

        foreach (var dirName in installedNodeVersions)
        {
            var item = new ToolStripMenuItem(dirName, null, (s, e) =>
            {
                _config.ActiveNode = dirName;
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                NodeManager.UpdateCurrentJunction(_appRoot, dirName);
                if (_lblNodeTitle != null) _lblNodeTitle.Text = $"Active: Node.js {_config.ActiveNode}";
                _refreshNodePackages?.Invoke();
                BuildTrayMenu();
                lblStatusText.Text = $"Node.js {dirName} active.";
            })
            {
                Checked = dirName.Equals(_config.ActiveNode, StringComparison.OrdinalIgnoreCase)
            };
            nodeSubMenu.DropDownItems.Add(item);
        }
        trayMenu.Items.Add(nodeSubMenu);

        // Auto-start Enabled Services on Launch
        var autoStartItem = new ToolStripMenuItem("Auto-start on Launch", null, (s, e) =>
        {
            _config.AutoStartServices = !_config.AutoStartServices;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            if (s is ToolStripMenuItem mi) mi.Checked = _config.AutoStartServices;
        })
        {
            Checked = _config.AutoStartServices
        };
        trayMenu.Items.Add(autoStartItem);

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
            })
            {
                Checked = getter()
            };
            enabledServicesMenu.DropDownItems.Add(item);
        }

        AddServiceEnabledMenuItem("Nginx", "nginx", () => _config.EnableNginx, v => _config.EnableNginx = v);
        AddServiceEnabledMenuItem("MySQL", "mysql", () => _config.EnableMysql, v => _config.EnableMysql = v);
        AddServiceEnabledMenuItem("Mailpit", "mailpit", () => _config.EnableMailpit, v => _config.EnableMailpit = v);
        AddServiceEnabledMenuItem("PostgreSQL", "postgresql", () => _config.EnablePostgresql, v => _config.EnablePostgresql = v);
        AddServiceEnabledMenuItem("Redis", "redis", () => _config.EnableRedis, v => _config.EnableRedis = v);
        AddServiceEnabledMenuItem("RustFS (S3)", "rustfs", () => _config.EnableRustFs, v => _config.EnableRustFs = v);

        trayMenu.Items.Add(enabledServicesMenu);

        // Start with Windows option
        var winLogonItem = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
        {
            _config.StartWithWindows = !_config.StartWithWindows;
            WindowsStartup.SetStartup(_config.StartWithWindows, Application.ExecutablePath);
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            if (s is ToolStripMenuItem mi) mi.Checked = _config.StartWithWindows;
        })
        {
            Checked = _config.StartWithWindows
        };
        trayMenu.Items.Add(winLogonItem);

        trayMenu.Items.Add(new ToolStripSeparator());

        // Quick Tools in Tray
        trayMenu.Items.Add(new ToolStripMenuItem("Open Web Root (/www)", null, BtnOpenWww_Click));
        trayMenu.Items.Add(new ToolStripMenuItem("Open Mailpit Inbox", null, BtnOpenMailpit_Click));
        trayMenu.Items.Add(new ToolStripMenuItem("Database Client...", null, (s, e) => OpenDatabaseClient("mysql", false)));
        trayMenu.Items.Add(new ToolStripMenuItem("Open Terminal", null, BtnOpenTerminal_Click));
        trayMenu.Items.Add(new ToolStripMenuItem("Check for Updates...", null, BtnCheckUpdates_Click));
        trayMenu.Items.Add(new ToolStripSeparator());

        // Master Service Actions
        trayMenu.Items.Add(new ToolStripMenuItem("Start All Services", null, async (s, e) => await StartAllServicesAsync()));
        trayMenu.Items.Add(new ToolStripMenuItem("Stop All Services", null, async (s, e) => await StopAllServicesAsync()));
        trayMenu.Items.Add(new ToolStripSeparator());

        trayMenu.Items.Add(new ToolStripMenuItem("Exit Dev Lite Server", null, async (s, e) => await ExitApplicationAsync()));

        notifyIcon.ContextMenuStrip = trayMenu;
    }

    private async Task StartAllServicesAsync()
    {
        btnStartAll.Enabled = false;
        lblStatusText.Text = "Starting all enabled services...";
        lblStatusText.ForeColor = ModernColors.Warning;

        // Auto-sync hosts file for any newly added *.test projects if needed (only when Zero-UAC local DNS is not active)
        if (!DnsServer.IsZeroUacActive && VirtualHostManager.NeedsHostsSync(_appRoot))
        {
            lblStatusText.Text = "Syncing *.test virtual hosts to hosts file...";
            await VirtualHostManager.SyncHostsFileBatchAsync(_appRoot);
        }

        // Auto-start PHP FastCGI first if enabled and installed
        if (_config.EnablePhp && _php.IsInstalled && _php.Status != ServiceStatus.Running)
        {
            await _php.StartAsync();
        }

        foreach (var svc in _services)
        {
            if (svc != _php && _config.IsServiceEnabled(svc.Name) && svc.IsInstalled && svc.Status != ServiceStatus.Running)
            {
                await svc.StartAsync();
            }
        }

        btnStartAll.Enabled = true;
        lblStatusText.Text = "All enabled services started.";
        lblStatusText.ForeColor = ModernColors.Success;
        UpdateServiceStats();
    }

    private async Task StopAllServicesAsync()
    {
        btnStopAll.Enabled = false;
        lblStatusText.Text = "Stopping all services...";
        lblStatusText.ForeColor = ModernColors.Warning;

        var runningServices = _services
            .Where(s => s.Status == ServiceStatus.Running || s.Status == ServiceStatus.Starting)
            .ToList();

        if (runningServices.Count > 0)
        {
            await Task.WhenAll(runningServices.Select(s => s.StopAsync()));
        }

        btnStopAll.Enabled = true;
        lblStatusText.Text = "All services stopped.";
        lblStatusText.ForeColor = ModernColors.TextSecondary;
        UpdateServiceStats();
    }

    private async Task AutoStartConfiguredServicesAsync()
    {
        if (!_config.AutoStartServices) return;

        btnStartAll.Enabled = false;
        try
        {
            // Auto-start PHP FastCGI first if enabled and installed
            if (_config.EnablePhp && _php.IsInstalled && _php.Status != ServiceStatus.Running)
            {
                await _php.StartAsync();
            }

            foreach (var svc in _services)
            {
                if (svc != _php && _config.IsServiceEnabled(svc.Name) && svc.IsInstalled && svc.Status != ServiceStatus.Running)
                {
                    await svc.StartAsync();
                }
            }
        }
        catch (Exception ex)
        {
            lblStatusText.Text = $"⚠ Auto-start error: {ex.Message}";
            lblStatusText.ForeColor = ModernColors.Danger;
        }
        finally
        {
            btnStartAll.Enabled = true;
            UpdateServiceStats();
        }
    }

    private async void BtnStartAll_Click(object? sender, EventArgs e)
    {
        int runningCount = _services.Count(s => s != _php && _config.IsServiceEnabled(s.Name) && s.Status == ServiceStatus.Running);
        if (runningCount > 0)
        {
            await StopAllServicesAsync();
        }
        else
        {
            await StartAllServicesAsync();
        }
    }
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
        WorkerManager.StopAllWorkers();
        SchedulerManager.StopAll();
        CloudflareTunnelManager.StopAllTunnels();
        DnsServer.Instance.Stop();
        _job.Dispose();
        notifyIcon.Visible = false;
        _isShuttingDown = true;
        Interlocked.Exchange(ref _shutdownCleanupDone, 1);
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
        OpenDatabaseClient("mysql", false);
    }

    private async void OpenDatabaseClient(string serviceKey, bool forceDialog = false)
    {
        string defaultPref = serviceKey.ToLowerInvariant() switch
        {
            "mysql" => _config.DefaultMySqlClient,
            "postgresql" or "postgres" => _config.DefaultPgClient,
            "redis" => _config.DefaultRedisClient,
            _ => "ask"
        };

        var available = DatabaseClientManager.GetClientsForService(serviceKey, _appRoot);
        var candidate = available.FirstOrDefault(c => c.Id.Equals(defaultPref, StringComparison.OrdinalIgnoreCase) && c.IsInstalled);

        if (!forceDialog && candidate != null)
        {
            await DatabaseClientManager.LaunchClientAsync(candidate, serviceKey, _appRoot, _config, _nginx, _php, s =>
            {
                lblStatusText.Text = s;
                lblStatusText.ForeColor = ModernColors.Primary;
            });
            return;
        }

        using var dlg = new DbClientDialog(serviceKey, _appRoot, _config);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedClient != null)
        {
            if (dlg.RememberChoice)
            {
                switch (serviceKey.ToLowerInvariant())
                {
                    case "mysql":
                        _config.DefaultMySqlClient = dlg.SelectedClient.Id;
                        break;
                    case "postgresql" or "postgres":
                        _config.DefaultPgClient = dlg.SelectedClient.Id;
                        break;
                    case "redis":
                        _config.DefaultRedisClient = dlg.SelectedClient.Id;
                        break;
                }
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            }

            await DatabaseClientManager.LaunchClientAsync(dlg.SelectedClient, serviceKey, _appRoot, _config, _nginx, _php, s =>
            {
                lblStatusText.Text = s;
                lblStatusText.ForeColor = ModernColors.Primary;
            });
        }
    }

    private async Task<int?> PromptPortConflictResolverAsync(string serviceName, int currentPort, int suggestedPort)
    {
        if (InvokeRequired)
        {
            return await (Task<int?>)Invoke(new Func<Task<int?>>(() => PromptPortConflictResolverAsync(serviceName, currentPort, suggestedPort)));
        }

        using var dlg = new PortConflictDialog(serviceName, currentPort, suggestedPort);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            int newPort = dlg.SelectedPort;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            RefreshAllServiceCards();
            BuildTrayMenu();
            lblStatusText.Text = $"Port for {serviceName} reallocated to {newPort}.";
            lblStatusText.ForeColor = ModernColors.Success;
            return newPort;
        }

        return null;
    }

    private void RefreshAllServiceCards()
    {
        foreach (Control c in cardContainer.Controls)
        {
            if (c is ServiceCard sc)
            {
                sc.RefreshInfo();
            }
        }
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
                        "Could not check for updates. Please check your internet connection.",
                        "Check for Updates",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                return;
            }

            if (!updateInfo.IsUpdateAvailable)
            {
                if (manual)
                {
                    lblStatusText.Text = $"Up to date! Running latest v{updateInfo.CurrentVersion}";
                    lblStatusText.ForeColor = ModernColors.Success;
                    MessageBox.Show(
                        this,
                        $"You are running the latest version (v{updateInfo.CurrentVersion}).",
                        "Up to Date",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                return;
            }

            // Update is available! Prompt user
            lblStatusText.Text = $"Update available: v{updateInfo.LatestVersion}";
            lblStatusText.ForeColor = ModernColors.Warning;

            using var dialog = new UpdateDialog(updateInfo, async () =>
            {
                await StopAllServicesAsync();
                _job.Dispose();
            });
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                lblStatusText.Text = "Stopping all services before installing update...";
                await StopAllServicesAsync();
                _job.Dispose();
                notifyIcon.Visible = false;
                Application.Exit();
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

    // ==========================================
    // WINDOW LIFECYCLE & TRAY MANAGEMENT
    // ==========================================
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Fires when form is shown normally (not --tray mode).
        // Tray-mode startup is handled in SetVisibleCore via BeginInvoke.
        await RunStartupTasksAsync();
    }

    private async Task RunStartupTasksAsync()
    {
        if (_hasRunStartupTasks) return;
        _hasRunStartupTasks = true;

        if (_config.AutoStartServices && !_hasAutoStartedServices)
        {
            _hasAutoStartedServices = true;
            await AutoStartConfiguredServicesAsync();
        }

        WorkerManager.StartAllAutoWorkers(_appRoot, _job);
        SchedulerManager.StartAutoTasks(_appRoot, _job);

        if (_config.CheckUpdatesOnStart)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await CheckForUpdatesAsync(manual: false);
            });
        }
    }

    protected override void SetVisibleCore(bool value)
    {
        if (!_allowVisible)
        {
            value = false;
            if (!IsHandleCreated)
            {
                CreateHandle();
                // OnLoad never fires when form stays permanently hidden (--tray mode).
                // Schedule startup tasks via BeginInvoke so they run on the message loop
                // after the handle and all controls are fully initialized.
                BeginInvoke(new Action(async () => await RunStartupTasksAsync()));
            }
        }
        base.SetVisibleCore(value);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isExitingExplicitly) return;

        if (e.CloseReason == CloseReason.UserClosing && _config.MinimizeToTray)
        {
            e.Cancel = true;
            _allowVisible = false;
            Hide();
            notifyIcon.ShowBalloonTip(
                2000,
                "Dev Lite Server",
                "Application is still running in the system tray.",
                ToolTipIcon.Info
            );
        }
        else
        {
            e.Cancel = false;
            PerformGracefulShutdown(3500);
        }
    }

    private void PerformGracefulShutdown(int timeoutMs = 3500)
    {
        if (Interlocked.Exchange(ref _shutdownCleanupDone, 1) != 0) return;

        _isShuttingDown = true;
        _isExitingExplicitly = true;
        try { notifyIcon.Visible = false; } catch { }

        bool blockReasonSet = false;
        if (IsHandleCreated && !IsDisposed)
        {
            try { blockReasonSet = ShutdownBlockReasonCreate(Handle, "Stopping DevLiteServer services cleanly..."); } catch { }
        }

        try
        {
            var tasks = new List<Task>();
            foreach (var svc in _services)
            {
                if (svc.Status == ServiceStatus.Running || svc.Status == ServiceStatus.Starting)
                {
                    var captured = svc;
                    tasks.Add(Task.Run(async () =>
                    {
                        try { await captured.StopAsync(); } catch { }
                    }));
                }
            }

            tasks.Add(Task.Run(() =>
            {
                try { WorkerManager.StopAllWorkers(); } catch { }
                try { SchedulerManager.StopAll(); } catch { }
                try { CloudflareTunnelManager.StopAllTunnels(); } catch { }
                try { DnsServer.Instance.Stop(); } catch { }
            }));

            Task.WhenAll(tasks).Wait(timeoutMs);
            try { _job.Dispose(); } catch { }
        }
        finally
        {
            if (blockReasonSet && IsHandleCreated && !IsDisposed)
            {
                try { ShutdownBlockReasonDestroy(Handle); } catch { }
            }
        }
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e) => RestoreFromTray();

    public void RestoreFromTray()
    {
        _allowVisible = true;
        Show();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
        BringToFront();
        SingleInstance.ForceForeground(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_QUERYENDSESSION)
        {
            m.Result = new IntPtr(1);
            return;
        }
        if (m.Msg == WM_ENDSESSION)
        {
            if (m.WParam != IntPtr.Zero)
            {
                PerformGracefulShutdown(3500);
            }
            base.WndProc(ref m);
            return;
        }
        if (SingleInstance.IsRestoreMessage(m.Msg))
        {
            RestoreFromTray();
            return;
        }
        base.WndProc(ref m);
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
