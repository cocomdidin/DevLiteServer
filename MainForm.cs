using System.Diagnostics;
using System.Drawing.Drawing2D;
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

    // Sites Page dynamic container
    private FlowLayoutPanel _pnlSitesContainer = null!;

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
        navGeneral.IsActive = (index == 1);
        navSites.IsActive = (index == 2);
        navPhp.IsActive = (index == 3);
        navNode.IsActive = (index == 4);
        navServices.IsActive = (index == 5);
        navMail.IsActive = (index == 6);

        pageDashboard.Visible = (index == 0);
        pageGeneral.Visible = (index == 1);
        pageSites.Visible = (index == 2);
        pagePhp.Visible = (index == 3);
        pageNode.Visible = (index == 4);
        pageServices.Visible = (index == 5);
        pageMail.Visible = (index == 6);

        switch (index)
        {
            case 0:
                lblPageTitle.Text = "Dashboard";
                lblPageSubtitle.Text = "Environment overview and service controller";
                break;
            case 1:
                lblPageTitle.Text = "General Settings";
                lblPageSubtitle.Text = "System integration and preference settings";
                break;
            case 2:
                lblPageTitle.Text = "Sites & Local Domains";
                lblPageSubtitle.Text = "Virtual hosts and project directories in /www";
                RefreshSitesList();
                break;
            case 3:
                lblPageTitle.Text = "PHP Environment";
                lblPageSubtitle.Text = "Active FastCGI runtime and extension configuration";
                break;
            case 4:
                lblPageTitle.Text = "Node.js Environment";
                lblPageSubtitle.Text = "Node runtime, npm package manager, and CLI tools";
                break;
            case 5:
                lblPageTitle.Text = "Database & Cache Services";
                lblPageSubtitle.Text = "MySQL, PostgreSQL, and Redis daemon configuration";
                break;
            case 6:
                lblPageTitle.Text = "Mail Testing";
                lblPageSubtitle.Text = "Mailpit local SMTP capture daemon and webmail inbox";
                break;
        }

        LayoutTopRibbonButtons();
    }

    // ==========================================
    // INITIALIZE THE 7 PAGES
    // ==========================================
    private void InitPages()
    {
        Panel[] pages = [pageDashboard, pageGeneral, pageSites, pagePhp, pageNode, pageServices, pageMail];
        foreach (var page in pages)
        {
            page.Dock = DockStyle.Fill;
            page.BackColor = ModernColors.Background;
            page.AutoScroll = true;
            page.Visible = false;
            pageContainer.Controls.Add(page);
        }

        PopulateDashboardPage();
        PopulateGeneralPage();
        PopulateSitesPage();
        PopulatePhpPage();
        PopulateNodePage();
        PopulateServicesPage();
        PopulateMailPage();
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
    // PAGE 1: GENERAL
    // ------------------------------------------
    private void PopulateGeneralPage()
    {
        pageGeneral.Padding = new Padding(24, 16, 24, 20);

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

        // 3. Auto-start enabled services
        var togAuto = new ModernToggle { Checked = _config.AutoStartServices };
        togAuto.CheckedChanged += (s, e) =>
        {
            _config.AutoStartServices = togAuto.Checked;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        };
        var cardAuto = CreateOptionCard("Auto-start Enabled Services", "Start all enabled server daemons immediately when the app launches.", togAuto);

        // 4. Check for updates on startup
        var togUpdates = new ModernToggle { Checked = _config.CheckUpdatesOnStart };
        togUpdates.CheckedChanged += (s, e) =>
        {
            _config.CheckUpdatesOnStart = togUpdates.Checked;
            ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
        };
        var cardUpdates = CreateOptionCard("Check for Updates on Startup", "Check official GitHub releases for new versions upon startup.", togUpdates);

        // 5. Open Full Settings Dialog Button
        var pnlSettingsLink = new Panel
        {
            Height = 52,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 8, 0, 0)
        };
        var btnFullSettings = new ModernButton
        {
            Text = "Configure All Ports & Full Settings",
            IconKind = IconKind.Gear,
            IconSize = 12,
            Width = 240,
            Height = 36,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(0, 8)
        };
        btnFullSettings.Click += (s, e) =>
        {
            using var dlg = new SettingsForm(_config, _appRoot);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                BuildServiceCards();
                BuildTrayMenu();
                lblStatusText.Text = "Settings applied successfully.";
                lblStatusText.ForeColor = ModernColors.Success;
            }
        };
        pnlSettingsLink.Controls.Add(btnFullSettings);

        // Add to general page in reverse dock order so pnlHeader is on top
        pageGeneral.Controls.AddRange([pnlSettingsLink, cardUpdates, cardAuto, cardTray, cardWin, pnlHeader]);
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
            Location = new Point(0, 4)
        };
        btnOpenFolder.Click += BtnOpenWww_Click;

        var btnRefresh = new ModernButton
        {
            Text = "Refresh Sites",
            IconKind = IconKind.Refresh,
            IconSize = 10,
            Width = 115,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(145, 4)
        };
        btnRefresh.Click += (s, e) => RefreshSitesList();

        pnlTop.Controls.Add(btnOpenFolder);
        pnlTop.Controls.Add(btnRefresh);

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
        var rootCard = CreateSiteCard("localhost", $"http://localhost:{_config.HttpPort}", wwwDir, cardWidth);
        _pnlSitesContainer.Controls.Add(rootCard);

        // 2. Subproject directories in /www
        var subDirs = Directory.GetDirectories(wwwDir);
        foreach (var dir in subDirs)
        {
            string dirName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(dirName) || dirName.StartsWith(".")) continue;

            string url = $"http://localhost:{_config.HttpPort}/{dirName}";
            var siteCard = CreateSiteCard(dirName, url, dir, cardWidth);
            _pnlSitesContainer.Controls.Add(siteCard);
        }
    }

    private Panel CreateSiteCard(string name, string url, string folderPath, int width)
    {
        var card = new Panel
        {
            Width = width,
            Height = 68,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
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

            // Globe badge
            var badgeRect = new RectangleF(12, (cardH - 34) / 2f, 34, 34);
            using var badgeBg = new SolidBrush(ModernColors.Card);
            using var badgeBorder = new Pen(ModernColors.BorderSubtle, 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(20, (cardH - 34) / 2f + 8, 18, 18);
            VectorIcons.Draw(g, IconKind.Globe, iconRect, ModernColors.Primary);
        };

        var lblName = new Label
        {
            Text = name,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
            Location = new Point(56, 9),
            AutoSize = true
        };

        var lblUrl = new Label
        {
            Text = $"{url}  •  {folderPath}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8f),
            Location = new Point(57, 31),
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
            try { Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true }); } catch { }
        };

        void LayoutSiteButtons()
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
        card.Resize += (s, e) => LayoutSiteButtons();
        LayoutSiteButtons();

        card.Controls.Add(lblName);
        card.Controls.Add(lblUrl);
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
            Height = 150,
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
        var cmbVersions = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Width = 140,
            Location = new Point(110, 13)
        };

        string phpRoot = Path.Combine(_appRoot, "bin", "php");
        if (Directory.Exists(phpRoot))
        {
            var dirs = Directory.GetDirectories(phpRoot).Select(Path.GetFileName).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            if (dirs.Length > 0) cmbVersions.Items.AddRange(dirs!);
        }
        if (cmbVersions.Items.Count == 0) cmbVersions.Items.Add(_config.ActivePhp);
        cmbVersions.SelectedItem = _config.ActivePhp;

        cmbVersions.SelectedIndexChanged += async (s, e) =>
        {
            if (cmbVersions.SelectedItem is string newVer && !newVer.Equals(_config.ActivePhp, StringComparison.OrdinalIgnoreCase))
            {
                _config.ActivePhp = newVer;
                ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);
                if (_php.Status == ServiceStatus.Running)
                {
                    lblStatusText.Text = $"Switching PHP to {newVer}...";
                    await _php.RestartAsync();
                    lblStatusText.Text = $"Switched to PHP {newVer}.";
                }
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
            Text = "Open php.ini",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Width = 110,
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

        var lblPhpDetails = new Label
        {
            Text = $"Loopback Address: 127.0.0.1:{_config.PhpFastCgiPort}\nSupervisor Pool: Max 5000 requests with instant auto-respawn\nDefault extensions: curl, mysqli, pdo_mysql, pdo_pgsql, pgsql, redis, mbstring, openssl, zip",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(16, 56),
            Size = new Size(540, 50)
        };

        heroCard.Controls.Add(lblPhpActiveTitle);
        heroCard.Controls.Add(cmbVersions);
        heroCard.Controls.Add(btnRestartPhp);
        heroCard.Controls.Add(btnOpenPhpIni);
        heroCard.Controls.Add(lblPhpDetails);

        pagePhp.Controls.Add(heroCard);
        pagePhp.Controls.Add(pnlHeader);
        pnlHeader.SendToBack();
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

        var lblNodeTitle = new Label
        {
            Text = $"Bundled Runtime: {_config.ActiveNode}",
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

        var lblNodeNote = new Label
        {
            Text = "Node.js, npm, and npx are accessible via Dev Lite Server Isolated Terminal.\nNo global Windows PATH modification is made.",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(16, 88),
            AutoSize = true
        };

        heroCard.Controls.Add(lblNodeTitle);
        heroCard.Controls.Add(btnOpenNodeTerminal);
        heroCard.Controls.Add(lblNodeNote);

        pageNode.Controls.Add(heroCard);
        pageNode.Controls.Add(pnlHeader);
        pnlHeader.SendToBack();
    }

    // ------------------------------------------
    // PAGE 5: SERVICES (Databases & Cache)
    // ------------------------------------------
    private void PopulateServicesPage()
    {
        pageServices.Padding = new Padding(24, 16, 24, 20);

        var pnlHeader = CreateSectionHeader("Databases & Cache Engines", "Relational databases and in-memory key-value cache services.");

        // 1. MySQL Card
        var cardMySql = CreateServiceManagementCard(_mysql, "Default user: 'root' with empty password (port 3306).", () =>
        {
            BtnOpenAdminer_Click(null, EventArgs.Empty);
        });

        // 2. PostgreSQL Card
        var cardPg = CreateServiceManagementCard(_postgresql, "Default user: 'postgres' with trust auth (port 5432).", () =>
        {
            BtnOpenAdminer_Click(null, EventArgs.Empty);
        });

        // 3. Redis Card
        var cardRedis = CreateServiceManagementCard(_redis, "In-memory key-value store binding to loopback 127.0.0.1:6379.", () =>
        {
            TerminalLauncher.OpenTerminal(_appRoot, _config);
        });

        pageServices.Controls.AddRange([cardRedis, cardPg, cardMySql, pnlHeader]);
        pnlHeader.SendToBack();
    }

    private Panel CreateServiceManagementCard(IService service, string description, Action onOpenClient)
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
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
            Location = new Point(14, 24)
        };
        service.StatusChanged += (s, st) =>
        {
            if (InvokeRequired) Invoke(() => pill.Status = st);
            else pill.Status = st;
        };

        var lblName = new Label
        {
            Text = $"{service.Name}  (Port: {service.Port})",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(120, 14),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = description,
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(121, 38),
            AutoSize = true
        };

        var btnToggle = new ModernButton
        {
            Text = service.Status == ServiceStatus.Running ? "Stop" : "Start",
            IconKind = service.Status == ServiceStatus.Running ? IconKind.Stop : IconKind.Play,
            IconSize = 10,
            Width = 84,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = service.Status == ServiceStatus.Running ? ModernColors.Danger : ModernColors.Success,
            HoverColor = service.Status == ServiceStatus.Running ? ModernColors.DangerHover : ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };

        btnToggle.Click += async (s, e) =>
        {
            btnToggle.Enabled = false;
            if (service.Status == ServiceStatus.Running) await service.StopAsync();
            else await service.StartAsync();
            btnToggle.Enabled = true;
        };

        service.StatusChanged += (s, st) =>
        {
            void UpdateBtn()
            {
                btnToggle.Text = st == ServiceStatus.Running ? "Stop" : "Start";
                btnToggle.IconKind = st == ServiceStatus.Running ? IconKind.Stop : IconKind.Play;
                btnToggle.NormalColor = st == ServiceStatus.Running ? ModernColors.Danger : ModernColors.Success;
                btnToggle.HoverColor = st == ServiceStatus.Running ? ModernColors.DangerHover : ModernColors.SuccessHover;
            }
            if (InvokeRequired) Invoke((Action)UpdateBtn);
            else UpdateBtn();
        };

        var btnClient = new ModernButton
        {
            Text = "DB Client",
            IconKind = IconKind.Database,
            IconSize = 10,
            Width = 90,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnClient.Click += (s, e) => onOpenClient();

        void LayoutServiceButtons()
        {
            int cardH = card.Height - 10;
            int rightX = card.Width > 200 ? card.Width - btnToggle.Width - 14 : 500;
            btnToggle.Location = new Point(rightX, (cardH - btnToggle.Height) / 2);
            btnClient.Location = new Point(btnToggle.Left - btnClient.Width - 8, (cardH - btnClient.Height) / 2);

            int maxDescW = btnClient.Left - lblDesc.Left - 10;
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
            Height = 120,
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

        var lblMailTitle = new Label
        {
            Text = "Mailpit Local SMTP & Inbox Server",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = new Point(16, 14),
            AutoSize = true
        };

        var btnOpenInbox = new ModernButton
        {
            Text = "Open Webmail Inbox (:8025)",
            IconKind = IconKind.Mail,
            IconSize = 11,
            Width = 190,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(16, 46)
        };
        btnOpenInbox.Click += BtnOpenMailpit_Click;

        var lblMailNote = new Label
        {
            Text = $"SMTP Loopback: 127.0.0.1:{_config.MailpitSmtpPort}  •  Webmail Viewer: http://localhost:{_config.MailpitWebPort}",
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(16, 88),
            AutoSize = true
        };

        heroCard.Controls.Add(lblMailTitle);
        heroCard.Controls.Add(btnOpenInbox);
        heroCard.Controls.Add(lblMailNote);

        // Env Snippet Card
        var snippetCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 160,
            BackColor = ModernColors.Surface,
            Margin = new Padding(0, 14, 0, 0),
            Padding = new Padding(18, 14, 18, 14)
        };
        snippetCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, snippetCard.Width - 1f, snippetCard.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 8f);
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

        var txtSnippet = new TextBox
        {
            Text = $"MAIL_MAILER=smtp\r\nMAIL_HOST=127.0.0.1\r\nMAIL_PORT={_config.MailpitSmtpPort}\r\nMAIL_USERNAME=null\r\nMAIL_PASSWORD=null\r\nMAIL_ENCRYPTION=null",
            Multiline = true,
            ReadOnly = true,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderStyle = BorderStyle.None,
            Font = new Font("Cascadia Code", 8.5f),
            Location = new Point(16, 36),
            Size = new Size(500, 105)
        };

        snippetCard.Controls.Add(lblSnippetTitle);
        snippetCard.Controls.Add(txtSnippet);

        pageMail.Controls.AddRange([snippetCard, heroCard, pnlHeader]);
        pnlHeader.SendToBack();
    }

    // ==========================================
    // UI REUSABLE CARD HELPERS
    // ==========================================
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

        int enabledCount = _services.Count(s => _config.IsServiceEnabled(s.Name));
        int runningCount = _services.Count(s => _config.IsServiceEnabled(s.Name) && s.Status == ServiceStatus.Running);

        lblStatsText.Text = $"Services: {runningCount} / {enabledCount} running";
        lblStatsText.ForeColor = runningCount > 0 ? ModernColors.Success : ModernColors.TextMuted;
        lblStatsText.Location = new Point(footerPanel.ClientSize.Width - lblStatsText.Width - 20, 7);

        lblSidebarStatus.Text = runningCount > 0 ? $"{runningCount} running" : "Stopped";
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
                Text = "No services are currently enabled.",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = ModernColors.TextPrimary,
                Location = new Point(20, 40),
                AutoSize = true
            };
            var lblEmptySub = new Label
            {
                Text = "Use the 'General' tab or Settings dialog to enable Nginx, PHP, MySQL, Mailpit, etc.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ModernColors.TextSecondary,
                Location = new Point(20, 68),
                AutoSize = true
            };
            var btnGoGeneral = new ModernButton
            {
                Text = "Open General Settings",
                IconKind = IconKind.Gear,
                IconSize = 11,
                Width = 160,
                Height = 32,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.Primary,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Location = new Point(20, 100)
            };
            btnGoGeneral.Click += (s, e) => SelectNavTab(1);

            pnlEmpty.Controls.Add(lblEmptyTitle);
            pnlEmpty.Controls.Add(lblEmptySub);
            pnlEmpty.Controls.Add(btnGoGeneral);
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

        // Auto-start PHP FastCGI first if enabled
        if (_config.EnablePhp && _php.Status != ServiceStatus.Running)
        {
            await _php.StartAsync();
        }

        foreach (var svc in _services)
        {
            if (svc != _php && _config.IsServiceEnabled(svc.Name) && svc.Status != ServiceStatus.Running)
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
        if (_config.EnablePhp && _config.AutoStartPhp && _php.Status != ServiceStatus.Running)
        {
            await _php.StartAsync();
        }

        if (_config.EnableNginx && _config.AutoStartNginx && _nginx.Status != ServiceStatus.Running)
        {
            await _nginx.StartAsync();
        }

        if (_config.EnableMysql && _config.AutoStartMysql && _mysql.Status != ServiceStatus.Running)
        {
            await _mysql.StartAsync();
        }

        if (_config.EnableMailpit && _config.AutoStartMailpit && _mailpit.Status != ServiceStatus.Running)
        {
            await _mailpit.StartAsync();
        }

        if (_config.EnablePostgresql && _config.AutoStartPostgresql && _postgresql.Status != ServiceStatus.Running)
        {
            await _postgresql.StartAsync();
        }

        if (_config.EnableRedis && _config.AutoStartRedis && _redis.Status != ServiceStatus.Running)
        {
            await _redis.StartAsync();
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
