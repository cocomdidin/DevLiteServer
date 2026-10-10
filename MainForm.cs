using System.Diagnostics;
using System.Drawing.Drawing2D;
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
    private readonly List<IService> _services = new();

    private readonly bool _startMinimized;
    private bool _hasBeenShown = false;
    private bool _isExitingExplicitly = false;

    // Sites Page dynamic container
    private FlowLayoutPanel _pnlSitesContainer = null!;
    private Panel _pnlPhpPackages = null!;
    private Panel _pnlNodePackages = null!;
    private ComboBox _cmbPhpVersions = null!;
    private Label _lblNodeTitle = null!;

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
            svc.PortConflictResolver = PromptPortConflictResolverAsync;
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

        InitPages();
        BuildServiceCards();
        BuildTrayMenu();
        UpdateServiceStats();
        SelectNavTab(0);

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

        Shown += async (s, e) =>
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

    // ==========================================
    // NAVIGATION & TAB SWITCHING
    // ==========================================
    private void SelectNavTab(int index)
    {
        navDashboard.IsActive = (index == 0);
        navSites.IsActive = (index == 1);
        navPhp.IsActive = (index == 2);
        navNode.IsActive = (index == 3);
        navServices.IsActive = (index == 4);
        navMail.IsActive = (index == 5);
        navSettings.IsActive = (index == 6);

        pageDashboard.Visible = (index == 0);
        pageSites.Visible = (index == 1);
        pagePhp.Visible = (index == 2);
        pageNode.Visible = (index == 3);
        pageServices.Visible = (index == 4);
        pageMail.Visible = (index == 5);
        pageSettings.Visible = (index == 6);

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
                lblPageTitle.Text = "PHP Environment";
                lblPageSubtitle.Text = "Active FastCGI runtime and extension configuration";
                break;
            case 3:
                lblPageTitle.Text = "Node.js Environment";
                lblPageSubtitle.Text = "Node runtime, npm package manager, and CLI tools";
                break;
            case 4:
                lblPageTitle.Text = "Database & Cache Services";
                lblPageSubtitle.Text = "MySQL, PostgreSQL, and Redis daemon configuration";
                break;
            case 5:
                lblPageTitle.Text = "Mail Testing";
                lblPageSubtitle.Text = "Mailpit local SMTP capture daemon and webmail inbox";
                break;
            case 6:
                lblPageTitle.Text = "Settings";
                lblPageSubtitle.Text = "System integration, tray behavior, and automation preferences";
                break;
        }

        LayoutTopRibbonButtons();
    }

    // ==========================================
    // INITIALIZE THE 7 PAGES
    // ==========================================
    private void InitPages()
    {
        Panel[] pages = [pageDashboard, pageSites, pagePhp, pageNode, pageServices, pageMail, pageSettings];
        foreach (var page in pages)
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
        PopulateMailPage();
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
    private void PopulateSettingsPage()
    {
        pageSettings.Padding = new Padding(24, 16, 24, 20);

        var pnlHeader = CreateSectionHeader("Preferences & Automation", "Configure system integration, tray behavior, and auto-start rules.");

        // 1. Start on Windows Logon
        var togWindows = new ModernToggle { Checked = _config.StartWithWindows };
        togWindows.CheckedChanged += (s, e) =>
        {
            _config.StartWithWindows = togWindows.Checked;
            WindowsStartup.SetStartup(_config.StartWithWindows, Application.ExecutablePath);
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        };
        var cardWin = CreateOptionCard("Start with Windows Logon", "Automatically launch Dev Lite Server into system tray when logging in.", togWindows);

        // 2. Minimize to Tray
        var togTray = new ModernToggle { Checked = _config.MinimizeToTray };
        togTray.CheckedChanged += (s, e) =>
        {
            _config.MinimizeToTray = togTray.Checked;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        };
        var cardTray = CreateOptionCard("Minimize to System Tray", "Closing the main window keeps services running in the background tray.", togTray);

        // 4. Auto-start enabled services on launch
        var togAuto = new ModernToggle { Checked = _config.AutoStartServices };
        togAuto.CheckedChanged += (s, e) =>
        {
            _config.AutoStartServices = togAuto.Checked;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            BuildTrayMenu();
        };
        var cardAuto = CreateOptionCard("Auto-start Enabled Services", "Automatically launch all enabled services when Dev Lite Server opens.", togAuto);

        // 6. Action Buttons Bar (Reset Settings & Open config.ini)
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

        pnlActions.Controls.Add(btnOpenConfig);
        pnlActions.Controls.Add(btnResetDefaults);

        // Add to settings page in reverse dock order so pnlHeader is on top
        pageSettings.Controls.AddRange([pnlActions, cardAuto, cardTray, cardWin, pnlHeader]);
        pnlHeader.SendToBack();
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
            Height = 50,
            Padding = new Padding(0, 0, 0, 12)
        };

        var btnAddSite = new ModernButton
        {
            Text = "+ Add Site",
            IconKind = IconKind.Plus,
            IconSize = 10,
            Width = 96,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(0, 4)
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

        var btnOpenFolder = new ModernButton
        {
            Text = "Open /www Folder",
            IconKind = IconKind.Folder,
            IconSize = 11,
            Width = 135,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(102, 4)
        };
        btnOpenFolder.Click += BtnOpenWww_Click;

        var btnRefresh = new ModernButton
        {
            Text = "Refresh Sites",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 110,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(243, 4)
        };
        btnRefresh.Click += (s, e) => RefreshSitesList();

        var btnSyncHosts = new ModernButton
        {
            Text = "Sync Hosts",
            IconKind = IconKind.Lightning,
            IconSize = 10,
            Width = 105,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(359, 4)
        };
        btnSyncHosts.Click += async (s, e) =>
        {
            btnSyncHosts.Enabled = false;
            lblStatusText.Text = "Syncing *.test virtual domains to Windows hosts file...";
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
                lblStatusText.Text = "Failed or canceled hosts sync.";
                lblStatusText.ForeColor = ModernColors.Warning;
            }
            btnSyncHosts.Enabled = true;
        };

        var btnOpenConf = new ModernButton
        {
            Text = "nginx.conf",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 95,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(470, 4)
        };
        btnOpenConf.Click += (s, e) =>
        {
            string confFile = Path.Combine(_nginx.GetNginxDirectory(), "conf", "nginx.conf");
            if (File.Exists(confFile))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{confFile}\"") { UseShellExecute = true });
            }
        };

        var lblHttps = new Label
        {
            Text = "HTTPS:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            AutoSize = true
        };
        var txtHttps = CreatePortInput(_config.HttpsPort, p =>
        {
            _config.HttpsPort = p;
            VirtualHostManager.GenerateVhostsConfig(_appRoot, _config, Path.Combine(_nginx.GetNginxDirectory(), "conf"));
            RefreshSitesList();
        });

        var lblHttp = new Label
        {
            Text = "HTTP:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            AutoSize = true
        };
        var txtHttp = CreatePortInput(_config.HttpPort, p =>
        {
            _config.HttpPort = p;
            _nginx.UpdatePort(p);
            RefreshSitesList();
        });

        void LayoutSitesTop()
        {
            int rX = pnlTop.ClientSize.Width > 200 ? pnlTop.ClientSize.Width - txtHttps.Width - 10 : 640;
            txtHttps.Location = new Point(rX, 8);
            lblHttps.Location = new Point(txtHttps.Left - lblHttps.Width - 5, 12);

            txtHttp.Location = new Point(lblHttps.Left - txtHttp.Width - 14, 8);
            lblHttp.Location = new Point(txtHttp.Left - lblHttp.Width - 5, 12);
        }
        pnlTop.Resize += (s, e) => LayoutSitesTop();
        LayoutSitesTop();

        pnlTop.Controls.Add(btnAddSite);
        pnlTop.Controls.Add(btnOpenFolder);
        pnlTop.Controls.Add(btnRefresh);
        pnlTop.Controls.Add(btnSyncHosts);
        pnlTop.Controls.Add(btnOpenConf);
        pnlTop.Controls.Add(lblHttp);
        pnlTop.Controls.Add(txtHttp);
        pnlTop.Controls.Add(lblHttps);
        pnlTop.Controls.Add(txtHttps);

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
            using var bgBrush = new SolidBrush(ModernColors.Surface);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);

            // Badge
            var badgeRect = new RectangleF(14, (cardH - 34) / 2f, 34, 34);
            using var badgeBg = new SolidBrush(ModernColors.Card);
            using var badgeBorder = new Pen(ModernColors.BorderSubtle, 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(22, (cardH - 34) / 2f + 8, 18, 18);
            VectorIcons.Draw(g, IconKind.Server, iconRect, ModernColors.Primary);
        };

        var lblName = new Label
        {
            Text = "localhost",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(60, 10),
            AutoSize = true
        };

        var lblUrl = new Label
        {
            Text = $"{url}  •  Default Web Root  •  {wwwDir}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(61, 33),
            AutoSize = true
        };

        var btnBrowse = new ModernButton
        {
            Text = "Open",
            IconKind = IconKind.Globe,
            IconSize = 10,
            Width = 72,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnBrowse.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        };

        var btnOpenDir = new ModernButton
        {
            Text = "Folder",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 72,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnOpenDir.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", wwwDir) { UseShellExecute = true }); } catch { }
        };

        void LayoutRootSiteButtons()
        {
            int cardH = card.Height - 10;
            int rightX = card.Width > 200 ? card.Width - btnBrowse.Width - 14 : 500;
            btnBrowse.Location = new Point(rightX, (cardH - btnBrowse.Height) / 2);
            btnOpenDir.Location = new Point(btnBrowse.Left - btnOpenDir.Width - 8, (cardH - btnOpenDir.Height) / 2);

            int maxUrlW = btnOpenDir.Left - lblUrl.Left - 10;
            if (maxUrlW > 50)
            {
                lblUrl.MaximumSize = new Size(maxUrlW, 20);
                lblUrl.AutoEllipsis = true;
            }
        }
        card.Resize += (s, e) => LayoutRootSiteButtons();
        LayoutRootSiteButtons();

        card.Controls.Add(lblName);
        card.Controls.Add(lblUrl);
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
            using var bgBrush = new SolidBrush(ModernColors.Surface);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);

            // Icon circle badge
            var badgeRect = new RectangleF(14, (cardH - 34) / 2f, 34, 34);
            using var badgeBg = new SolidBrush(site.SslEnabled ? Color.FromArgb(16, 36, 32) : ModernColors.Card);
            using var badgeBorder = new Pen(site.SslEnabled ? Color.FromArgb(34, 197, 94, 120) : ModernColors.BorderSubtle, 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(22, (cardH - 34) / 2f + 8, 18, 18);
            if (site.SslEnabled)
            {
                VectorIcons.Draw(g, IconKind.Lock, iconRect, ModernColors.Success);
            }
            else
            {
                VectorIcons.Draw(g, IconKind.Globe, iconRect, ModernColors.Primary);
            }
        };

        var lblName = new Label
        {
            Text = site.Domain,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(60, 10),
            AutoSize = true
        };

        string phpDesc = string.IsNullOrEmpty(site.PhpVersion) || site.PhpVersion.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? $"PHP {_config.ActivePhp} (Default)"
            : site.PhpVersion;

        string sslDesc = site.SslEnabled ? "SSL (443)" : "HTTP";
        string locationDesc = site.IsExternal ? "External" : (site.HasPublicSubfolder ? "public/" : "Local");

        var lblUrl = new Label
        {
            Text = $"{url}  •  {sslDesc}  •  {phpDesc}  •  {locationDesc}  •  {site.DocumentRoot}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(61, 33),
            AutoSize = true
        };

        var btnBrowse = new ModernButton
        {
            Text = "Open",
            IconKind = IconKind.Globe,
            IconSize = 10,
            Width = 68,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = site.SslEnabled ? ModernColors.Success : ModernColors.Primary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnBrowse.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        };

        var btnOpenDir = new ModernButton
        {
            Text = "Folder",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 68,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
        };
        btnOpenDir.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", site.PhysicalPath) { UseShellExecute = true }); } catch { }
        };

        var btnEdit = new ModernButton
        {
            Text = "Edit",
            IconKind = IconKind.Pencil,
            IconSize = 10,
            Width = 60,
            Height = 28,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold)
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

        ModernButton? btnDelete = null;
        if (site.IsExternal)
        {
            btnDelete = new ModernButton
            {
                Text = "Remove",
                IconKind = IconKind.Trash,
                IconSize = 10,
                Width = 68,
                Height = 28,
                BorderRadius = 5,
                ShowBorder = true,
                BorderLineColor = Color.FromArgb(244, 63, 94, 120),
                NormalColor = ModernColors.Card,
                HoverColor = Color.FromArgb(45, 20, 28),
                ForeColor = Color.FromArgb(254, 205, 211),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
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
            card.Controls.Add(btnDelete);
        }

        void LayoutSiteButtons()
        {
            int cardH = card.Height - 10;
            int rightX = card.Width > 200 ? card.Width - btnBrowse.Width - 14 : 500;
            btnBrowse.Location = new Point(rightX, (cardH - btnBrowse.Height) / 2);
            btnOpenDir.Location = new Point(btnBrowse.Left - btnOpenDir.Width - 6, (cardH - btnOpenDir.Height) / 2);
            btnEdit.Location = new Point(btnOpenDir.Left - btnEdit.Width - 6, (cardH - btnEdit.Height) / 2);

            int leftBoundary = btnEdit.Left;
            if (btnDelete != null)
            {
                btnDelete.Location = new Point(btnEdit.Left - btnDelete.Width - 6, (cardH - btnDelete.Height) / 2);
                leftBoundary = btnDelete.Left;
            }

            int maxUrlW = leftBoundary - lblUrl.Left - 10;
            if (maxUrlW > 50)
            {
                lblUrl.MaximumSize = new Size(maxUrlW, 20);
                lblUrl.AutoEllipsis = true;
            }
        }
        card.Resize += (s, e) => LayoutSiteButtons();
        LayoutSiteButtons();

        card.Controls.Add(lblName);
        card.Controls.Add(lblUrl);
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
        pagePhp.Padding = new Padding(24, 16, 24, 20);

        var pnlHeader = CreateSectionHeader("PHP FastCGI Runtime", "Multi-version PHP environment managed by FastCGI worker supervisor.");

        // Hero Card
        var heroCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 175,
            BackColor = ModernColors.Surface,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(18, 14, 18, 14)
        };
        heroCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, heroCard.Width - 1f, heroCard.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        var lblPhpActiveTitle = new Label
        {
            Text = "Active Version:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(16, 16),
            AutoSize = true
        };

        // ComboBox versions
        _cmbPhpVersions = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Width = 140,
            Location = new Point(110, 13)
        };

        void RefreshPhpVersionsCombo()
        {
            _cmbPhpVersions.Items.Clear();
            string phpRoot = Path.Combine(_appRoot, "bin", "php");
            if (Directory.Exists(phpRoot))
            {
                var dirs = Directory.GetDirectories(phpRoot).Select(Path.GetFileName).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (dirs.Length > 0) _cmbPhpVersions.Items.AddRange(dirs!);
            }
            if (_cmbPhpVersions.Items.Count == 0) _cmbPhpVersions.Items.Add(_config.ActivePhp);
            if (_cmbPhpVersions.Items.Contains(_config.ActivePhp))
                _cmbPhpVersions.SelectedItem = _config.ActivePhp;
            else if (_cmbPhpVersions.Items.Count > 0)
                _cmbPhpVersions.SelectedIndex = 0;
        }
        RefreshPhpVersionsCombo();

        _cmbPhpVersions.SelectedIndexChanged += async (s, e) =>
        {
            if (_cmbPhpVersions.SelectedItem is string newVer && !newVer.Equals(_config.ActivePhp, StringComparison.OrdinalIgnoreCase))
            {
                _config.ActivePhp = newVer;
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                if (_php.Status == ServiceStatus.Running)
                {
                    lblStatusText.Text = $"Switching PHP to {newVer}...";
                    await _php.RestartAsync();
                    lblStatusText.Text = $"Switched to PHP {newVer}.";
                }
                RefreshPhpPackagesList();
                BuildTrayMenu();
            }
        };

        var btnRestartPhp = new ModernButton
        {
            Text = "Restart FastCGI",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 120,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(265, 12)
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
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 84,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(395, 12)
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
            Text = "Open /ext",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 90,
            Height = 30,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(488, 12)
        };
        btnOpenExt.Click += (s, e) =>
        {
            string extDir = Path.Combine(_php.GetPhpDirectory(), "ext");
            if (Directory.Exists(extDir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{extDir}\"") { UseShellExecute = true });
            }
        };

        // Row 2: Port configuration
        var lblPortTitle = new Label
        {
            Text = "FastCGI Port:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(16, 56),
            AutoSize = true
        };
        var txtFastCgiPort = CreatePortInput(_config.PhpFastCgiPort, p =>
        {
            _config.PhpFastCgiPort = p;
            _php.UpdatePort(p);
        });
        txtFastCgiPort.Location = new Point(102, 53);

        var lblPhpDetails = new Label
        {
            Text = "Loopback Address: 127.0.0.1  •  Pool: Max 5000 requests with instant worker respawn\nDefault extensions: curl, mysqli, pdo_mysql, pdo_pgsql, pgsql, redis, mbstring, openssl, zip",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(16, 92),
            Size = new Size(580, 40)
        };

        heroCard.Controls.Add(lblPhpActiveTitle);
        heroCard.Controls.Add(_cmbPhpVersions);
        heroCard.Controls.Add(btnRestartPhp);
        heroCard.Controls.Add(btnOpenPhpIni);
        heroCard.Controls.Add(btnOpenExt);
        heroCard.Controls.Add(lblPortTitle);
        heroCard.Controls.Add(txtFastCgiPort);
        heroCard.Controls.Add(lblPhpDetails);

        // Section: Available PHP Runtimes (Herd Downloader)
        var pnlPhpPacksHeader = CreateSectionHeader("Available PHP Runtimes (Official windows.php.net)", "1-click download, auto-extract, and configure PHP versions.");

        _pnlPhpPackages = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        void RefreshPhpPackagesList()
        {
            _pnlPhpPackages.Controls.Clear();
            var packages = PackageCatalog.GetPhpPackages();
            var cards = new List<Control>();
            foreach (var pkg in packages)
            {
                var card = new PackageRowCard(pkg, _appRoot, _config, async () =>
                {
                    RefreshPhpVersionsCombo();
                    RefreshPhpPackagesList();
                    BuildTrayMenu();
                    if (_php.Status == ServiceStatus.Running)
                    {
                        lblStatusText.Text = $"Applying PHP {_config.ActivePhp}...";
                        await _php.RestartAsync();
                        lblStatusText.Text = $"PHP {_config.ActivePhp} active.";
                    }
                });
                cards.Add(card);
            }
            cards.Reverse();
            _pnlPhpPackages.Controls.AddRange(cards.ToArray());
        }
        RefreshPhpPackagesList();

        pagePhp.Controls.AddRange([_pnlPhpPackages, pnlPhpPacksHeader, heroCard, pnlHeader]);
        pnlHeader.SendToBack();
        heroCard.SendToBack();
        pnlPhpPacksHeader.SendToBack();
        _pnlPhpPackages.SendToBack();
    }

    // ------------------------------------------
    // PAGE 4: NODE
    // ------------------------------------------
    private void PopulateNodePage()
    {
        pageNode.Padding = new Padding(24, 16, 24, 20);

        var pnlHeader = CreateSectionHeader("Node.js & JavaScript Runtime", "Portable Node.js environment with NPM & NPX pre-configured.");

        var heroCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 130,
            BackColor = ModernColors.Surface,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(18, 14, 18, 14)
        };
        heroCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, heroCard.Width - 1f, heroCard.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        _lblNodeTitle = new Label
        {
            Text = $"Active Runtime: {_config.ActiveNode}",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = new Point(16, 14),
            AutoSize = true
        };

        var btnOpenNodeTerminal = new ModernButton
        {
            Text = "Launch Node Terminal",
            IconKind = IconKind.Terminal,
            IconSize = 11,
            Width = 160,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(16, 48)
        };
        btnOpenNodeTerminal.Click += BtnOpenTerminal_Click;

        var btnOpenNodeFolder = new ModernButton
        {
            Text = "Node Directory",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 125,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(184, 48)
        };
        btnOpenNodeFolder.Click += (s, e) =>
        {
            string nodeDir = Path.Combine(_appRoot, "bin", "nodejs", _config.ActiveNode);
            if (!Directory.Exists(nodeDir)) nodeDir = Path.Combine(_appRoot, "bin", "nodejs");
            if (Directory.Exists(nodeDir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{nodeDir}\"") { UseShellExecute = true });
            }
        };

        var lblNodeNote = new Label
        {
            Text = "Node.js, npm, and npx are accessible via Dev Lite Server Isolated Terminal.\nNo global Windows PATH modification is made.",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(16, 88),
            AutoSize = true
        };

        heroCard.Controls.Add(_lblNodeTitle);
        heroCard.Controls.Add(btnOpenNodeTerminal);
        heroCard.Controls.Add(btnOpenNodeFolder);
        heroCard.Controls.Add(lblNodeNote);

        // Section: Available Node.js Runtimes (Herd Downloader)
        var pnlNodePacksHeader = CreateSectionHeader("Available Node.js Runtimes (Official nodejs.org)", "1-click download and extract portable Node.js, NPM, and NPX runtimes.");

        _pnlNodePackages = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        void RefreshNodePackagesList()
        {
            _pnlNodePackages.Controls.Clear();
            var packages = PackageCatalog.GetNodePackages();
            var cards = new List<Control>();
            foreach (var pkg in packages)
            {
                var card = new PackageRowCard(pkg, _appRoot, _config, () =>
                {
                    _lblNodeTitle.Text = $"Active Runtime: {_config.ActiveNode}";
                    RefreshNodePackagesList();
                    BuildTrayMenu();
                    lblStatusText.Text = $"Node.js {_config.ActiveNode} active.";
                });
                cards.Add(card);
            }
            cards.Reverse();
            _pnlNodePackages.Controls.AddRange(cards.ToArray());
        }
        RefreshNodePackagesList();

        pageNode.Controls.AddRange([_pnlNodePackages, pnlNodePacksHeader, heroCard, pnlHeader]);
        pnlHeader.SendToBack();
        heroCard.SendToBack();
        pnlNodePacksHeader.SendToBack();
        _pnlNodePackages.SendToBack();
    }

    // ------------------------------------------
    // PAGE 5: SERVICES (Databases & Cache)
    // ------------------------------------------
    private void PopulateServicesPage()
    {
        pageServices.Padding = new Padding(24, 16, 24, 20);

        var pnlHeader = CreateSectionHeader("Databases & Cache Engines", "Relational databases and in-memory key-value cache services.");

        // 1. MySQL Card
        var cardMySql = CreateServiceManagementCard(
            _mysql,
            "MySQL 8.4 Server (Default user: 'root' with empty password)",
            () => _config.EnableMysql,
            v => _config.EnableMysql = v,
            () => _config.MysqlPort,
            p => _config.MysqlPort = p,
            Path.Combine(_appRoot, "bin", "mysql", "data"),
            () => BtnOpenAdminer_Click(null, EventArgs.Empty));

        // 2. PostgreSQL Card
        var cardPg = CreateServiceManagementCard(
            _postgresql,
            "PostgreSQL 17 Server (Default user: 'postgres' with trust auth)",
            () => _config.EnablePostgresql,
            v => _config.EnablePostgresql = v,
            () => _config.PostgreSqlPort,
            p => _config.PostgreSqlPort = p,
            Path.Combine(_appRoot, "bin", "postgresql", "data"),
            () => BtnOpenAdminer_Click(null, EventArgs.Empty));

        // 3. Redis Card
        var cardRedis = CreateServiceManagementCard(
            _redis,
            "In-memory key-value cache server binding to loopback 127.0.0.1",
            () => _config.EnableRedis,
            v => _config.EnableRedis = v,
            () => _config.RedisPort,
            p => _config.RedisPort = p,
            null,
            () => TerminalLauncher.OpenTerminal(_appRoot, _config));

        pageServices.Controls.AddRange([cardRedis, cardPg, cardMySql, pnlHeader]);
        pnlHeader.SendToBack();
    }

    private Panel CreateServiceManagementCard(
        IService service,
        string description,
        Func<bool> getEnabled,
        Action<bool> setEnabled,
        Func<int> getPort,
        Action<int> setPort,
        string? dataDirPath,
        Action onOpenClient)
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
            if (InvokeRequired) Invoke(() => pill.Status = st);
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
            if (InvokeRequired) Invoke((Action)UpdateBtn);
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
        btnClient.Click += (s, e) => onOpenClient();

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
    // PAGE 6: MAIL
    // ------------------------------------------
    private void PopulateMailPage()
    {
        pageMail.Padding = new Padding(24, 16, 24, 20);

        var pnlHeader = CreateSectionHeader("Mailpit Local Mail Testing", "Zero-configuration SMTP capture daemon and real-time webmail inbox.");

        // Hero Card
        var heroCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 156,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(16, 8, 16, 16)
        };
        heroCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = heroCard.Height - 10f;
            var rect = new RectangleF(halfStroke, halfStroke, heroCard.Width - stroke, cardH - stroke);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var bgBrush = new SolidBrush(ModernColors.Surface);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        var pill = new StatusPill
        {
            Status = _mailpit.Status,
            Location = new Point(14, 20)
        };
        _mailpit.StatusChanged += (s, st) =>
        {
            if (InvokeRequired) Invoke(() => pill.Status = st);
            else pill.Status = st;
        };

        var lblMailTitle = new Label
        {
            Text = "Mailpit Local SMTP & Inbox Server",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = new Point(116, 12),
            AutoSize = true
        };

        var lblMailDesc = new Label
        {
            Text = "Captures outgoing SMTP traffic and provides an instant webmail viewer.",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(117, 34),
            AutoSize = true
        };

        // Row 1 (Y=62): Enabled & Auto-Start
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
            Checked = _config.EnableMailpit,
            Location = new Point(170, 60)
        };
        togEnable.CheckedChanged += (s, e) =>
        {
            _config.EnableMailpit = togEnable.Checked;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
            BuildServiceCards();
            BuildTrayMenu();
        };

        // Action Buttons (Right-aligned)
        var btnToggle = new ModernButton
        {
            Text = _mailpit.Status == ServiceStatus.Running ? "Stop" : (_mailpit.Status == ServiceStatus.NotInstalled ? "Install" : "Start"),
            IconKind = _mailpit.Status == ServiceStatus.Running ? IconKind.Stop : (_mailpit.Status == ServiceStatus.NotInstalled ? IconKind.Download : IconKind.Play),
            IconSize = 10,
            Width = 80,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = _mailpit.Status == ServiceStatus.Running ? ModernColors.Danger : (_mailpit.Status == ServiceStatus.NotInstalled ? ModernColors.Primary : ModernColors.Success),
            HoverColor = _mailpit.Status == ServiceStatus.Running ? ModernColors.DangerHover : (_mailpit.Status == ServiceStatus.NotInstalled ? ModernColors.PrimaryHover : ModernColors.SuccessHover),
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        void UpdateMailBtn()
        {
            if (_mailpit.Status == ServiceStatus.Running)
            {
                btnToggle.Text = "Stop";
                btnToggle.IconKind = IconKind.Stop;
                btnToggle.NormalColor = ModernColors.Danger;
                btnToggle.HoverColor = ModernColors.DangerHover;
            }
            else if (_mailpit.Status == ServiceStatus.NotInstalled)
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
                if (_mailpit.Status == ServiceStatus.NotInstalled)
                {
                    await InstallServiceWithUiAsync("mailpit", _mailpit);
                }
                else if (_mailpit.Status == ServiceStatus.Running)
                {
                    await _mailpit.StopAsync();
                }
                else
                {
                    await _mailpit.StartAsync();
                }
            }
            finally
            {
                btnToggle.Enabled = true;
                UpdateMailBtn();
            }
        };

        _mailpit.StatusChanged += (s, st) =>
        {
            if (InvokeRequired) Invoke((Action)UpdateMailBtn);
            else UpdateMailBtn();
        };

        var btnOpenInbox = new ModernButton
        {
            Text = $"Open Inbox (:{_config.MailpitWebPort})",
            IconKind = IconKind.Mail,
            IconSize = 10,
            Width = 145,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnOpenInbox.Click += BtnOpenMailpit_Click;

        var btnTestMail = new ModernButton
        {
            Text = "Send Test",
            IconKind = IconKind.Lightning,
            IconSize = 10,
            Width = 92,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnTestMail.Click += async (s, e) =>
        {
            btnTestMail.Enabled = false;
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
                btnTestMail.Enabled = true;
            }
        };

        // Forward declare snippet update action
        Action? updateSnippet = null;

        // Row 2 (Y=100): SMTP & Web Ports
        var lblSmtp = new Label
        {
            Text = "SMTP Port:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(117, 103),
            AutoSize = true
        };
        var txtSmtp = CreatePortInput(_config.MailpitSmtpPort, p =>
        {
            _config.MailpitSmtpPort = p;
            updateSnippet?.Invoke();
        });
        txtSmtp.Location = new Point(190, 100);

        var lblWeb = new Label
        {
            Text = "Web UI Port:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(260, 103),
            AutoSize = true
        };
        var txtWeb = CreatePortInput(_config.MailpitWebPort, p =>
        {
            _config.MailpitWebPort = p;
            _mailpit.UpdatePort(p);
            btnOpenInbox.Text = $"Open Inbox (:{p})";
        });
        txtWeb.Location = new Point(345, 100);

        void LayoutMailButtons()
        {
            int cardH = heroCard.Height - 10;
            int rightX = heroCard.Width > 200 ? heroCard.Width - btnToggle.Width - 14 : 500;
            btnToggle.Location = new Point(rightX, (cardH - btnToggle.Height) / 2);
            btnOpenInbox.Location = new Point(btnToggle.Left - btnOpenInbox.Width - 8, (cardH - btnOpenInbox.Height) / 2);
            btnTestMail.Location = new Point(btnOpenInbox.Left - btnTestMail.Width - 8, (cardH - btnTestMail.Height) / 2);

            int leftLimit = btnTestMail.Left - 10;
            int maxDescW = leftLimit - lblMailDesc.Left;
            if (maxDescW > 50)
            {
                lblMailDesc.MaximumSize = new Size(maxDescW, 20);
                lblMailDesc.AutoEllipsis = true;
            }
        }
        heroCard.Resize += (s, e) => LayoutMailButtons();
        LayoutMailButtons();

        heroCard.Controls.Add(pill);
        heroCard.Controls.Add(lblMailTitle);
        heroCard.Controls.Add(lblMailDesc);
        heroCard.Controls.Add(lblEnable);
        heroCard.Controls.Add(togEnable);
        heroCard.Controls.Add(lblSmtp);
        heroCard.Controls.Add(txtSmtp);
        heroCard.Controls.Add(lblWeb);
        heroCard.Controls.Add(txtWeb);
        heroCard.Controls.Add(btnTestMail);
        heroCard.Controls.Add(btnOpenInbox);
        heroCard.Controls.Add(btnToggle);

        // Env Snippet Card
        var snippetCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 170,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 14, 0, 0),
            Padding = new Padding(16, 12, 16, 16)
        };
        snippetCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            float cardH = snippetCard.Height - 10f;
            var rect = new RectangleF(halfStroke, halfStroke, snippetCard.Width - stroke, cardH - stroke);
            using var path = CreateRoundedRectangle(rect, 8f);
            using var bgBrush = new SolidBrush(ModernColors.Surface);
            g.FillPath(bgBrush, path);
            using var pen = new Pen(ModernColors.BorderSubtle, 1f);
            g.DrawPath(pen, path);
        };

        var lblSnippetTitle = new Label
        {
            Text = "Laravel / Symfony / WordPress .env Configuration:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(16, 12),
            AutoSize = true
        };

        var btnCopyEnv = new ModernButton
        {
            Text = "Copy .env",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 86,
            Height = 26,
            BorderRadius = 4,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Location = new Point(500, 10)
        };

        var txtSnippet = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderStyle = BorderStyle.None,
            Font = new Font("Cascadia Code", 8.5f),
            Location = new Point(16, 40),
            Size = new Size(570, 105)
        };

        void RefreshSnippetText()
        {
            txtSnippet.Text = $"MAIL_MAILER=smtp\r\nMAIL_HOST=127.0.0.1\r\nMAIL_PORT={_config.MailpitSmtpPort}\r\nMAIL_USERNAME=null\r\nMAIL_PASSWORD=null\r\nMAIL_ENCRYPTION=null";
        }
        RefreshSnippetText();
        updateSnippet = RefreshSnippetText;

        btnCopyEnv.Click += async (s, e) =>
        {
            Clipboard.SetText(txtSnippet.Text);
            btnCopyEnv.Text = "Copied!";
            await Task.Delay(1500);
            btnCopyEnv.Text = "Copy .env";
        };

        snippetCard.Resize += (s, e) =>
        {
            btnCopyEnv.Location = new Point(Math.Max(300, snippetCard.Width - btnCopyEnv.Width - 18), 10);
            txtSnippet.Width = Math.Max(200, snippetCard.Width - 36);
        };

        snippetCard.Controls.Add(lblSnippetTitle);
        snippetCard.Controls.Add(btnCopyEnv);
        snippetCard.Controls.Add(txtSnippet);

        pageMail.Controls.AddRange([snippetCard, heroCard, pnlHeader]);
        pnlHeader.SendToBack();
    }

    // ==========================================
    // UI REUSABLE CARD HELPERS
    // ==========================================
    private TextBox CreatePortInput(int currentPort, Action<int> onPortSaved)
    {
        var txt = new TextBox
        {
            Text = currentPort.ToString(),
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.Primary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            Width = 56,
            Height = 24
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

    private static Panel CreateSectionHeader(string title, string subtitle)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(0, 0, 0, 8)
        };
        var lblT = new Label
        {
            Text = title,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = new Point(0, 0),
            AutoSize = true
        };
        var lblS = new Label
        {
            Text = subtitle,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(1, 22),
            AutoSize = true
        };
        pnl.Controls.Add(lblT);
        pnl.Controls.Add(lblS);
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

    private void UpdateServiceStats()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateServiceStats);
            return;
        }

        int enabledCount = _services.Count(s => s != _php && _config.IsServiceEnabled(s.Name));
        int runningCount = _services.Count(s => s != _php && _config.IsServiceEnabled(s.Name) && s.Status == ServiceStatus.Running);

        lblStatsText.Text = $"Services: {runningCount} / {enabledCount} running";
        lblStatsText.ForeColor = runningCount > 0 ? ModernColors.Success : ModernColors.TextMuted;
        lblStatsText.Location = new Point(footerPanel.ClientSize.Width - lblStatsText.Width - 20, 7);

        lblSidebarStatus.Text = runningCount > 0 ? $"{runningCount} running" : "Stopped";
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

    private void BuildServiceCards()
    {
        cardContainer.Controls.Clear();

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

        // Add enabled service cards in reverse dock order
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

        // Node Version submenu
        var nodeSubMenu = new ToolStripMenuItem("Node Version");
        string nodeRoot = Path.Combine(_appRoot, "bin", "nodejs");
        if (Directory.Exists(nodeRoot))
        {
            foreach (var dir in Directory.GetDirectories(nodeRoot))
            {
                string dirName = Path.GetFileName(dir);
                var item = new ToolStripMenuItem(dirName, null, (s, e) =>
                {
                    _config.ActiveNode = dirName;
                    ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                    if (_lblNodeTitle != null) _lblNodeTitle.Text = $"Active Runtime: {_config.ActiveNode}";
                    BuildTrayMenu();
                })
                {
                    Checked = dirName.Equals(_config.ActiveNode, StringComparison.OrdinalIgnoreCase)
                };
                nodeSubMenu.DropDownItems.Add(item);
            }
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

        // Auto-sync hosts file for any newly added *.test projects if needed
        if (VirtualHostManager.NeedsHostsSync(_appRoot))
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

        foreach (var svc in _services)
        {
            if (svc.Status == ServiceStatus.Running)
            {
                await svc.StopAsync();
            }
        }

        btnStopAll.Enabled = true;
        lblStatusText.Text = "All services stopped.";
        lblStatusText.ForeColor = ModernColors.TextSecondary;
        UpdateServiceStats();
    }

    private async Task AutoStartConfiguredServicesAsync()
    {
        if (!_config.AutoStartServices) return;

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
        string toolsDir = Path.Combine(_appRoot, "tools");
        if (Directory.Exists(toolsDir))
        {
            var exes = Directory.GetFiles(toolsDir, "*.exe", SearchOption.AllDirectories);
            if (exes.Length > 0)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exes[0]) { UseShellExecute = true });
                    return;
                }
                catch { }
            }
        }

        Process.Start(new ProcessStartInfo($"http://localhost:{_config.HttpPort}/adminer") { UseShellExecute = true });
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
    protected override void SetVisibleCore(bool value)
    {
        if (_startMinimized && !_hasBeenShown)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _hasBeenShown = true;
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isExitingExplicitly) return;

        if (e.CloseReason == CloseReason.UserClosing && _config.MinimizeToTray)
        {
            e.Cancel = true;
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
            _job.Dispose();
            notifyIcon.Visible = false;
        }
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e) => RestoreFromTray();

    public void RestoreFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
        BringToFront();
    }

    protected override void WndProc(ref Message m)
    {
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
