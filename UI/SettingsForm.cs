using System.Drawing.Drawing2D;
using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly string _appRoot;

    // Navigation state
    private int _selectedTabIndex = 0;
    private readonly List<NavButton> _navButtons = new();
    private readonly List<Panel> _tabPages = new();
    private Panel _pageContainer = null!;

    // Page 1: Services Toggles
    private ModernToggle _togNginx = null!;
    private ModernToggle _togPhp = null!;
    private ModernToggle _togMysql = null!;
    private ModernToggle _togMailpit = null!;
    private ModernToggle _togPostgresql = null!;
    private ModernToggle _togRedis = null!;

    // Page 2: Auto-Start Toggles
    private ModernToggle _togAutoMaster = null!;
    private ModernToggle _togAutoNginx = null!;
    private ModernToggle _togAutoPhp = null!;
    private ModernToggle _togAutoMysql = null!;
    private ModernToggle _togAutoMailpit = null!;
    private ModernToggle _togAutoPostgresql = null!;
    private ModernToggle _togAutoRedis = null!;
    private Panel _pnlAutoServicesGroup = null!;

    // Page 3: Network Ports TextBoxes
    private TextBox _txtHttpPort = null!;
    private TextBox _txtPhpPort = null!;
    private TextBox _txtMysqlPort = null!;
    private TextBox _txtMailpitWebPort = null!;
    private TextBox _txtMailpitSmtpPort = null!;
    private TextBox _txtPostgreSqlPort = null!;
    private TextBox _txtRedisPort = null!;

    // Page 4: General Toggles
    private ModernToggle _togStartWithWindows = null!;
    private ModernToggle _togMinimizeToTray = null!;
    private ModernToggle _togCheckUpdates = null!;

    public SettingsForm(AppConfig config, string appRoot)
    {
        _config = config;
        _appRoot = appRoot;

        InitializeComponent();
        SelectTab(0);
    }

    private void InitializeComponent()
    {
        Text = "Settings & Configuration - Dev Lite Server";
        ClientSize = new Size(680, 520);
        MinimumSize = new Size(680, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;

        // ==========================================
        // 1. TOP HEADER
        // ==========================================
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = ModernColors.Surface,
            Padding = new Padding(20, 10, 20, 10)
        };
        headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Header Icon Badge
            var badgeRect = new RectangleF(20, 12, 32, 32);
            using var badgeBg = new SolidBrush(Color.FromArgb(24, 38, 60));
            using var badgeBorder = new Pen(Color.FromArgb(56, 189, 248, 100), 1);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(28, 20, 16, 16);
            VectorIcons.Draw(g, IconKind.Gear, iconRect, ModernColors.Primary);

            // Bottom border
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblHeaderTitle = new Label
        {
            Text = "Settings & Preferences",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(62, 10)
        };

        var lblHeaderSub = new Label
        {
            Text = "Configure active services, startup automation, and network ports",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(63, 31)
        };

        headerPanel.Controls.Add(lblHeaderTitle);
        headerPanel.Controls.Add(lblHeaderSub);

        // ==========================================
        // 2. BOTTOM FOOTER
        // ==========================================
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = ModernColors.Surface,
            Padding = new Padding(20, 11, 20, 11)
        };
        footerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
        };

        var btnCancel = new ModernButton
        {
            Text = "Cancel",
            Width = 84,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Surface,
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(footerPanel.Width - 218, 11)
        };
        btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        var btnSave = new ModernButton
        {
            Text = "Save Changes",
            IconKind = IconKind.Check,
            IconSize = 10,
            Width = 118,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            PressedColor = ModernColors.SuccessBg,
            ForeColor = Color.White,
            Location = new Point(footerPanel.Width - 124, 11)
        };
        btnSave.Click += BtnSave_Click;

        footerPanel.Resize += (s, e) =>
        {
            btnSave.Location = new Point(footerPanel.Width - btnSave.Width - 20, 11);
            btnCancel.Location = new Point(btnSave.Left - btnCancel.Width - 10, 11);
        };

        footerPanel.Controls.Add(btnCancel);
        footerPanel.Controls.Add(btnSave);

        // ==========================================
        // 3. MAIN BODY (Sidebar + Page Container)
        // ==========================================
        var bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernColors.Background
        };

        // Left Sidebar Navigation
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 160,
            BackColor = ModernColors.Surface,
            Padding = new Padding(8, 14, 8, 14)
        };
        sidebar.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        AddNavTab(sidebar, 0, "Services", IconKind.Server);
        AddNavTab(sidebar, 1, "Auto-Start", IconKind.Lightning);
        AddNavTab(sidebar, 2, "Network Ports", IconKind.Globe);
        AddNavTab(sidebar, 3, "General", IconKind.Gear);

        // Right Page Container
        _pageContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernColors.Background,
            Padding = new Padding(20, 16, 20, 16)
        };

        // Create 4 Content Pages
        _tabPages.Add(CreateServicesPage());
        _tabPages.Add(CreateAutoStartPage());
        _tabPages.Add(CreatePortsPage());
        _tabPages.Add(CreateGeneralPage());

        foreach (var page in _tabPages)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _pageContainer.Controls.Add(page);
        }

        bodyPanel.Controls.Add(_pageContainer);
        bodyPanel.Controls.Add(sidebar);

        Controls.Add(bodyPanel);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);
    }

    private void AddNavTab(Panel sidebar, int index, string title, IconKind icon)
    {
        var btn = new NavButton
        {
            Title = title,
            Icon = icon,
            Width = 144,
            Height = 38,
            Location = new Point(8, 14 + (index * 42))
        };
        btn.Click += (s, e) => SelectTab(index);
        _navButtons.Add(btn);
        sidebar.Controls.Add(btn);
    }

    private void SelectTab(int index)
    {
        _selectedTabIndex = index;
        for (int i = 0; i < _navButtons.Count; i++)
        {
            _navButtons[i].IsActive = (i == index);
        }

        for (int i = 0; i < _tabPages.Count; i++)
        {
            _tabPages[i].Visible = (i == index);
        }
    }

    // ==========================================
    // PAGE 1: ACTIVE SERVICES
    // ==========================================
    private Panel CreateServicesPage()
    {
        var page = new Panel { AutoScroll = true };

        var lblTitle = CreatePageHeader("Active Services", "Choose which services are enabled in your environment. Disabled services are hidden from dashboard and skipped by Start All.");
        page.Controls.Add(lblTitle);

        var pnlList = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = 460
        };

        _togNginx = new ModernToggle { Checked = _config.EnableNginx };
        _togPhp = new ModernToggle { Checked = _config.EnablePhp };
        _togMysql = new ModernToggle { Checked = _config.EnableMysql };
        _togMailpit = new ModernToggle { Checked = _config.EnableMailpit };
        _togPostgresql = new ModernToggle { Checked = _config.EnablePostgresql };
        _togRedis = new ModernToggle { Checked = _config.EnableRedis };

        pnlList.Controls.Add(CreateSettingCard("Nginx Web Server", "High-performance reverse proxy & static file server (:80)", _togNginx, IconKind.Server));
        pnlList.Controls.Add(CreateSettingCard("PHP FastCGI", "Multi-version PHP worker pool supervisor (:9000)", _togPhp, IconKind.Lightning));
        pnlList.Controls.Add(CreateSettingCard("MySQL Database", "Zero-fuss local relational database (:3306)", _togMysql, IconKind.Database));
        pnlList.Controls.Add(CreateSettingCard("Mailpit Suite", "Local SMTP daemon (:1025) and Web inbox (:8025)", _togMailpit, IconKind.Mail));
        pnlList.Controls.Add(CreateSettingCard("PostgreSQL Database", "EDB PostgreSQL relational database server (:5432)", _togPostgresql, IconKind.Database));
        pnlList.Controls.Add(CreateSettingCard("Redis Cache", "In-memory key-value data store & fast cache (:6379)", _togRedis, IconKind.Server));

        page.Controls.Add(pnlList);
        return page;
    }

    // ==========================================
    // PAGE 2: AUTO-START AUTOMATION
    // ==========================================
    private Panel CreateAutoStartPage()
    {
        var page = new Panel { AutoScroll = true };

        var lblTitle = CreatePageHeader("Auto-Start Automation", "Configure which services start automatically in the background when Dev Lite Server launches.");
        page.Controls.Add(lblTitle);

        var pnlList = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = 460
        };

        _togAutoMaster = new ModernToggle { Checked = _config.AutoStartServices };

        var masterCard = CreateSettingCard(
            "Enable Auto-Start on Boot",
            "Master switch to boot selected services when application opens",
            _togAutoMaster,
            IconKind.Play,
            highlight: true
        );
        pnlList.Controls.Add(masterCard);

        _pnlAutoServicesGroup = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = 460,
            Margin = new Padding(0, 4, 0, 0)
        };

        _togAutoNginx = new ModernToggle { Checked = _config.AutoStartNginx };
        _togAutoPhp = new ModernToggle { Checked = _config.AutoStartPhp };
        _togAutoMysql = new ModernToggle { Checked = _config.AutoStartMysql };
        _togAutoMailpit = new ModernToggle { Checked = _config.AutoStartMailpit };
        _togAutoPostgresql = new ModernToggle { Checked = _config.AutoStartPostgresql };
        _togAutoRedis = new ModernToggle { Checked = _config.AutoStartRedis };

        _pnlAutoServicesGroup.Controls.Add(CreateSettingCard("Nginx", "Auto-start web server daemon", _togAutoNginx, IconKind.Server));
        _pnlAutoServicesGroup.Controls.Add(CreateSettingCard("PHP FastCGI", "Auto-start PHP FastCGI supervisor", _togAutoPhp, IconKind.Lightning));
        _pnlAutoServicesGroup.Controls.Add(CreateSettingCard("MySQL", "Auto-start MySQL 8.4 database server", _togAutoMysql, IconKind.Database));
        _pnlAutoServicesGroup.Controls.Add(CreateSettingCard("Mailpit", "Auto-start Mailpit local mail capture", _togAutoMailpit, IconKind.Mail));
        _pnlAutoServicesGroup.Controls.Add(CreateSettingCard("PostgreSQL", "Auto-start PostgreSQL database server", _togAutoPostgresql, IconKind.Database));
        _pnlAutoServicesGroup.Controls.Add(CreateSettingCard("Redis", "Auto-start Redis cache server", _togAutoRedis, IconKind.Server));

        void UpdateAutoSubGroup()
        {
            _pnlAutoServicesGroup.Enabled = _togAutoMaster.Checked;
        }

        _togAutoMaster.CheckedChanged += (s, e) => UpdateAutoSubGroup();
        UpdateAutoSubGroup();

        pnlList.Controls.Add(_pnlAutoServicesGroup);
        page.Controls.Add(pnlList);
        return page;
    }

    // ==========================================
    // PAGE 3: NETWORK PORTS
    // ==========================================
    private Panel CreatePortsPage()
    {
        var page = new Panel { AutoScroll = true };

        var lblTitle = CreatePageHeader("Network Ports", "Bind services to custom local ports. Default ports are pre-configured for standard local development.");
        page.Controls.Add(lblTitle);

        var portsContainer = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Width = 470
        };

        var (c1, t1) = CreatePortCard("Nginx HTTP", "Web server port (Default: 80)", _config.HttpPort);
        var (c2, t2) = CreatePortCard("PHP FastCGI", "FastCGI loopback (Default: 9000)", _config.PhpFastCgiPort);
        var (c3, t3) = CreatePortCard("MySQL Server", "Database port (Default: 3306)", _config.MysqlPort);
        var (c4, t4) = CreatePortCard("Mailpit Web", "Webmail viewer UI (Default: 8025)", _config.MailpitWebPort);
        var (c5, t5) = CreatePortCard("Mailpit SMTP", "SMTP capture port (Default: 1025)", _config.MailpitSmtpPort);
        var (c6, t6) = CreatePortCard("PostgreSQL", "Postgres port (Default: 5432)", _config.PostgreSqlPort);
        var (c7, t7) = CreatePortCard("Redis Cache", "Redis server port (Default: 6379)", _config.RedisPort);

        _txtHttpPort = t1;
        _txtPhpPort = t2;
        _txtMysqlPort = t3;
        _txtMailpitWebPort = t4;
        _txtMailpitSmtpPort = t5;
        _txtPostgreSqlPort = t6;
        _txtRedisPort = t7;

        portsContainer.Controls.AddRange([c1, c2, c3, c4, c5, c6, c7]);
        page.Controls.Add(portsContainer);
        return page;
    }

    // ==========================================
    // PAGE 4: GENERAL PREFERENCES
    // ==========================================
    private Panel CreateGeneralPage()
    {
        var page = new Panel { AutoScroll = true };

        var lblTitle = CreatePageHeader("General Preferences", "System integration, startup behavior, and application lifecycle preferences.");
        page.Controls.Add(lblTitle);

        var pnlList = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = 460
        };

        _togStartWithWindows = new ModernToggle { Checked = _config.StartWithWindows };
        _togMinimizeToTray = new ModernToggle { Checked = _config.MinimizeToTray };
        _togCheckUpdates = new ModernToggle { Checked = _config.CheckUpdatesOnStart };

        pnlList.Controls.Add(CreateSettingCard(
            "Start with Windows Logon",
            "Automatically launch Dev Lite Server minimized to tray on user login",
            _togStartWithWindows,
            IconKind.Power
        ));

        pnlList.Controls.Add(CreateSettingCard(
            "Minimize to System Tray",
            "Closing the main window minimizes to tray instead of stopping daemons",
            _togMinimizeToTray,
            IconKind.Globe
        ));

        pnlList.Controls.Add(CreateSettingCard(
            "Automatic Update Check",
            "Check for new releases on GitHub automatically when application launches",
            _togCheckUpdates,
            IconKind.Refresh
        ));

        page.Controls.Add(pnlList);
        return page;
    }

    // ==========================================
    // UI BUILDER HELPERS
    // ==========================================
    private static Panel CreatePageHeader(string title, string description)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Margin = new Padding(0, 0, 0, 10)
        };

        var lblMain = new Label
        {
            Text = title,
            ForeColor = ModernColors.Primary,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var lblSub = new Label
        {
            Text = description,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.25f),
            AutoSize = true,
            Location = new Point(0, 24),
            MaximumSize = new Size(460, 0)
        };

        pnl.Controls.Add(lblMain);
        pnl.Controls.Add(lblSub);
        return pnl;
    }

    private static Panel CreateSettingCard(string title, string description, Control rightControl, IconKind icon, bool highlight = false)
    {
        var card = new Panel
        {
            Width = 460,
            Height = 52,
            BackColor = highlight ? Color.FromArgb(16, 28, 44) : ModernColors.Surface,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(12, 8, 12, 8)
        };

        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float halfStroke = stroke / 2f;
            var rect = new RectangleF(halfStroke, halfStroke, card.Width - stroke, card.Height - stroke);

            using var path = CreateRoundedRectangle(rect, 6f);
            using var pen = new Pen(highlight ? Color.FromArgb(56, 189, 248, 120) : ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);

            // Icon badge on left
            var iconBadgeRect = new RectangleF(12, 12, 28, 28);
            using var badgeBg = new SolidBrush(ModernColors.Card);
            g.FillEllipse(badgeBg, iconBadgeRect);

            var iconRect = new RectangleF(18, 18, 16, 16);
            VectorIcons.Draw(g, icon, iconRect, highlight ? ModernColors.Primary : ModernColors.TextSecondary);
        };

        var lblTitle = new Label
        {
            Text = title,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(48, 8)
        };

        var lblDesc = new Label
        {
            Text = description,
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 7.75f),
            AutoSize = true,
            Location = new Point(49, 28),
            MaximumSize = new Size(330, 0)
        };

        rightControl.Location = new Point(card.Width - rightControl.Width - 14, (card.Height - rightControl.Height) / 2);

        card.Controls.Add(lblTitle);
        card.Controls.Add(lblDesc);
        card.Controls.Add(rightControl);

        return card;
    }

    private static (Panel card, TextBox textBox) CreatePortCard(string label, string note, int defaultPort)
    {
        var card = new Panel
        {
            Width = 224,
            Height = 62,
            BackColor = ModernColors.Surface,
            Margin = new Padding(0, 0, 10, 10),
            Padding = new Padding(10, 8, 10, 8)
        };

        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            var rect = new RectangleF(0.5f, 0.5f, card.Width - 1f, card.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var pen = new Pen(ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);
        };

        var lblTitle = new Label
        {
            Text = label,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(8, 6)
        };

        var lblNote = new Label
        {
            Text = note,
            ForeColor = ModernColors.TextMuted,
            Font = new Font("Segoe UI", 7.25f),
            AutoSize = true,
            Location = new Point(9, 22),
            MaximumSize = new Size(130, 0)
        };

        var txt = new TextBox
        {
            Text = defaultPort.ToString(),
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.Primary,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            Width = 62,
            Height = 26,
            Location = new Point(card.Width - 72, 16)
        };

        card.Controls.Add(lblTitle);
        card.Controls.Add(lblNote);
        card.Controls.Add(txt);

        return (card, txt);
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

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // Validate Ports
        if (!int.TryParse(_txtHttpPort.Text.Trim(), out int httpPort) || httpPort < 1 || httpPort > 65535 ||
            !int.TryParse(_txtPhpPort.Text.Trim(), out int phpPort) || phpPort < 1 || phpPort > 65535 ||
            !int.TryParse(_txtMysqlPort.Text.Trim(), out int mysqlPort) || mysqlPort < 1 || mysqlPort > 65535 ||
            !int.TryParse(_txtMailpitWebPort.Text.Trim(), out int mailWebPort) || mailWebPort < 1 || mailWebPort > 65535 ||
            !int.TryParse(_txtMailpitSmtpPort.Text.Trim(), out int mailSmtpPort) || mailSmtpPort < 1 || mailSmtpPort > 65535 ||
            !int.TryParse(_txtPostgreSqlPort.Text.Trim(), out int pgPort) || pgPort < 1 || pgPort > 65535 ||
            !int.TryParse(_txtRedisPort.Text.Trim(), out int redisPort) || redisPort < 1 || redisPort > 65535)
        {
            MessageBox.Show(
                this,
                "Please enter valid port numbers (between 1 and 65535).",
                "Invalid Port",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        // Apply Services
        _config.EnableNginx = _togNginx.Checked;
        _config.EnablePhp = _togPhp.Checked;
        _config.EnableMysql = _togMysql.Checked;
        _config.EnableMailpit = _togMailpit.Checked;
        _config.EnablePostgresql = _togPostgresql.Checked;
        _config.EnableRedis = _togRedis.Checked;

        // Apply Auto-Start
        _config.AutoStartServices = _togAutoMaster.Checked;
        _config.AutoStartNginx = _togAutoNginx.Checked;
        _config.AutoStartPhp = _togAutoPhp.Checked;
        _config.AutoStartMysql = _togAutoMysql.Checked;
        _config.AutoStartMailpit = _togAutoMailpit.Checked;
        _config.AutoStartPostgresql = _togAutoPostgresql.Checked;
        _config.AutoStartRedis = _togAutoRedis.Checked;

        // Apply General
        _config.StartWithWindows = _togStartWithWindows.Checked;
        _config.MinimizeToTray = _togMinimizeToTray.Checked;
        _config.CheckUpdatesOnStart = _togCheckUpdates.Checked;

        // Apply Ports
        _config.HttpPort = httpPort;
        _config.PhpFastCgiPort = phpPort;
        _config.MysqlPort = mysqlPort;
        _config.MailpitWebPort = mailWebPort;
        _config.MailpitSmtpPort = mailSmtpPort;
        _config.PostgreSqlPort = pgPort;
        _config.RedisPort = redisPort;

        // Sync Windows Startup
        WindowsStartup.SetStartup(_config.StartWithWindows, Application.ExecutablePath);

        // Save to config.ini
        ConfigManager.Save(Path.Combine(_appRoot, "config.ini"), _config);

        DialogResult = DialogResult.OK;
        Close();
    }

    // Inner class for modern sidebar nav buttons
    private class NavButton : Control
    {
        private bool _isActive;
        private bool _isHovered;

        public string Title { get; set; } = "";
        public IconKind Icon { get; set; } = IconKind.None;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    Invalidate();
                }
            }
        }

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bgColor = _isActive
                ? ModernColors.Card
                : (_isHovered ? ModernColors.SurfaceHover : Color.Transparent);

            using (var bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            if (_isActive)
            {
                // Active indicator line on left (3px)
                using var barBrush = new SolidBrush(ModernColors.Primary);
                g.FillRectangle(barBrush, 0, 4, 3, Height - 8);
            }

            // Draw Icon
            var iconRect = new RectangleF(14, (Height - 14) / 2f, 14, 14);
            Color iconColor = _isActive ? ModernColors.Primary : (_isHovered ? Color.White : ModernColors.TextSecondary);
            VectorIcons.Draw(g, Icon, iconRect, iconColor);

            // Draw Text
            Color textColor = _isActive ? Color.White : (_isHovered ? ModernColors.TextPrimary : ModernColors.TextSecondary);
            using var font = new Font("Segoe UI", 9f, _isActive ? FontStyle.Bold : FontStyle.Regular);
            var textRect = new Rectangle(36, 0, Width - 40, Height);
            TextRenderer.DrawText(
                g,
                Title,
                font,
                textRect,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            );
        }
    }
}
