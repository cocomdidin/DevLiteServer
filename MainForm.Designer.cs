using DevLiteServer.UI;

namespace DevLiteServer;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private System.Windows.Forms.NotifyIcon notifyIcon = null!;
    private System.Windows.Forms.ContextMenuStrip trayMenu = null!;

    // Sidebar
    private System.Windows.Forms.Panel sidebarPanel = null!;
    private System.Windows.Forms.Panel brandPanel = null!;
    private System.Windows.Forms.Panel navContainer = null!;
    private System.Windows.Forms.Panel sidebarFooter = null!;
    private System.Windows.Forms.Label lblSidebarStatus = null!;

    // Nav Buttons
    private NavButton navDashboard = null!;
    private NavButton navSites = null!;
    private NavButton navPhp = null!;
    private NavButton navNode = null!;
    private NavButton navServices = null!;
    private NavButton navMail = null!;
    private NavButton navSettings = null!;

    // Right Main Pane
    private System.Windows.Forms.Panel mainPane = null!;
    private System.Windows.Forms.Panel topRibbon = null!;
    private System.Windows.Forms.Label lblPageTitle = null!;
    private System.Windows.Forms.Label lblPageSubtitle = null!;

    // Top Action Buttons
    private ModernButton btnOpenWww = null!;
    private ModernButton btnOpenTerminal = null!;
    private ModernButton btnStartAll = null!;
    private ModernButton btnStopAll = null!;
    private ModernButton btnExit = null!;

    // Pages & Footer
    private System.Windows.Forms.Panel pageContainer = null!;
    private System.Windows.Forms.Panel footerPanel = null!;
    private System.Windows.Forms.Label lblStatusText = null!;
    private System.Windows.Forms.Label lblStatsText = null!;

    // Page Panels
    private System.Windows.Forms.Panel pageDashboard = null!;
    private System.Windows.Forms.Panel pageSites = null!;
    private System.Windows.Forms.Panel pagePhp = null!;
    private System.Windows.Forms.Panel pageNode = null!;
    private System.Windows.Forms.Panel pageServices = null!;
    private System.Windows.Forms.Panel pageMail = null!;
    private System.Windows.Forms.Panel pageSettings = null!;

    // Dashboard specific controls
    private System.Windows.Forms.Panel cardContainer = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        notifyIcon = new System.Windows.Forms.NotifyIcon(components);
        trayMenu = new System.Windows.Forms.ContextMenuStrip(components);

        // Sidebar Elements
        sidebarPanel = new System.Windows.Forms.Panel();
        brandPanel = new System.Windows.Forms.Panel();
        navContainer = new System.Windows.Forms.Panel();
        sidebarFooter = new System.Windows.Forms.Panel();
        lblSidebarStatus = new System.Windows.Forms.Label();

        navDashboard = new NavButton();
        navSites = new NavButton();
        navPhp = new NavButton();
        navNode = new NavButton();
        navServices = new NavButton();
        navMail = new NavButton();
        navSettings = new NavButton();

        // Right Main Pane Elements
        mainPane = new System.Windows.Forms.Panel();
        topRibbon = new System.Windows.Forms.Panel();
        lblPageTitle = new System.Windows.Forms.Label();
        lblPageSubtitle = new System.Windows.Forms.Label();

        btnOpenWww = new ModernButton();
        btnOpenTerminal = new ModernButton();
        btnStartAll = new ModernButton();
        btnStopAll = new ModernButton();
        btnExit = new ModernButton();

        pageContainer = new System.Windows.Forms.Panel();
        footerPanel = new System.Windows.Forms.Panel();
        lblStatusText = new System.Windows.Forms.Label();
        lblStatsText = new System.Windows.Forms.Label();

        // 7 Pages
        pageDashboard = new System.Windows.Forms.Panel();
        pageSites = new System.Windows.Forms.Panel();
        pagePhp = new System.Windows.Forms.Panel();
        pageNode = new System.Windows.Forms.Panel();
        pageServices = new System.Windows.Forms.Panel();
        pageMail = new System.Windows.Forms.Panel();
        pageSettings = new System.Windows.Forms.Panel();

        cardContainer = new System.Windows.Forms.Panel();

        SuspendLayout();

        // ==========================================
        // 1. LEFT SIDEBAR PANEL (Width: 195)
        // ==========================================
        sidebarPanel.Dock = DockStyle.Left;
        sidebarPanel.Width = 195;
        sidebarPanel.BackColor = ModernColors.Surface;
        sidebarPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, sidebarPanel.Width - 1, 0, sidebarPanel.Width - 1, sidebarPanel.Height);
        };

        // Brand Panel
        brandPanel.Dock = DockStyle.Top;
        brandPanel.Height = 64;
        brandPanel.BackColor = ModernColors.Surface;
        brandPanel.Padding = new Padding(16, 12, 16, 12);
        brandPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Brand Icon Circle Badge (34x34)
            var badgeRect = new RectangleF(14, 15, 34, 34);
            using var badgeBg = new SolidBrush(Color.FromArgb(20, 34, 52));
            using var badgeBorder = new Pen(Color.FromArgb(56, 189, 248, 140), 1.25f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(22, 23, 18, 18);
            VectorIcons.Draw(g, IconKind.Lightning, iconRect, ModernColors.Primary);

            // Title
            using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            TextRenderer.DrawText(g, "Dev Lite Server", titleFont, new Point(54, 15), ModernColors.TextPrimary);

            // "PORTABLE" pill
            var pillRect = new RectangleF(55, 35, 56, 15);
            using var pillBg = new SolidBrush(Color.FromArgb(24, 38, 60));
            using var pillPen = new Pen(Color.FromArgb(56, 189, 248, 90), 1f);
            using var pillPath = CreatePillPath(pillRect);
            g.FillPath(pillBg, pillPath);
            g.DrawPath(pillPen, pillPath);

            using var pillFont = new Font("Segoe UI", 6.75f, FontStyle.Bold);
            TextRenderer.DrawText(g, "PORTABLE", pillFont, Rectangle.Round(pillRect), ModernColors.Primary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // Bottom subtle divider
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, brandPanel.Height - 1, brandPanel.Width, brandPanel.Height - 1);
        };

        // Navigation Container
        navContainer.Dock = DockStyle.Fill;
        navContainer.BackColor = ModernColors.Surface;
        navContainer.Padding = new Padding(0, 10, 0, 10);
        navContainer.AutoScroll = true;

        // Nav Buttons (Added in top-to-bottom order)
        navSettings.Text = "Settings";
        navSettings.Icon = IconKind.Gear;
        navSettings.Dock = DockStyle.Top;
        navSettings.Click += (s, e) => SelectNavTab(6);

        navMail.Text = "Mail";
        navMail.Icon = IconKind.Mail;
        navMail.Dock = DockStyle.Top;
        navMail.Click += (s, e) => SelectNavTab(5);

        navServices.Text = "Services";
        navServices.Icon = IconKind.Database;
        navServices.Dock = DockStyle.Top;
        navServices.Click += (s, e) => SelectNavTab(4);

        navNode.Text = "Node";
        navNode.Icon = IconKind.Terminal;
        navNode.Dock = DockStyle.Top;
        navNode.Click += (s, e) => SelectNavTab(3);

        navPhp.Text = "PHP";
        navPhp.Icon = IconKind.Lightning;
        navPhp.Dock = DockStyle.Top;
        navPhp.Click += (s, e) => SelectNavTab(2);

        navSites.Text = "Sites";
        navSites.Icon = IconKind.Globe;
        navSites.Dock = DockStyle.Top;
        navSites.Click += (s, e) => SelectNavTab(1);

        navDashboard.Text = "Dashboard";
        navDashboard.Icon = IconKind.Server;
        navDashboard.IsActive = true;
        navDashboard.Dock = DockStyle.Top;
        navDashboard.Click += (s, e) => SelectNavTab(0);

        // Reverse dock order for WinForms DockStyle.Top
        navContainer.Controls.AddRange([navSettings, navMail, navServices, navNode, navPhp, navSites, navDashboard]);

        // Sidebar Footer
        sidebarFooter.Dock = DockStyle.Bottom;
        sidebarFooter.Height = 40;
        sidebarFooter.BackColor = ModernColors.Surface;
        sidebarFooter.Padding = new Padding(14, 8, 14, 8);
        sidebarFooter.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, sidebarFooter.Width, 0);

            // Active green indicator dot
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var dotBrush = new SolidBrush(ModernColors.Success);
            g.FillEllipse(dotBrush, 14, 16, 7, 7);
        };

        lblSidebarStatus.Text = "Environment Ready";
        lblSidebarStatus.ForeColor = ModernColors.TextSecondary;
        lblSidebarStatus.Font = new Font("Segoe UI", 7.75f, FontStyle.Regular);
        lblSidebarStatus.Location = new Point(27, 12);
        lblSidebarStatus.AutoSize = true;
        sidebarFooter.Controls.Add(lblSidebarStatus);

        sidebarPanel.Controls.Add(navContainer);
        sidebarPanel.Controls.Add(brandPanel);
        sidebarPanel.Controls.Add(sidebarFooter);

        // ==========================================
        // 2. RIGHT MAIN PANE
        // ==========================================
        mainPane.Dock = DockStyle.Fill;
        mainPane.BackColor = ModernColors.Background;

        // Top Ribbon Header (Height: 60)
        topRibbon.Dock = DockStyle.Top;
        topRibbon.Height = 60;
        topRibbon.BackColor = ModernColors.Surface;
        topRibbon.Padding = new Padding(20, 10, 20, 10);
        topRibbon.Resize += (s, e) => LayoutTopRibbonButtons();
        topRibbon.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, topRibbon.Height - 1, topRibbon.Width, topRibbon.Height - 1);
        };

        lblPageTitle.Text = "Dashboard";
        lblPageTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        lblPageTitle.ForeColor = ModernColors.TextPrimary;
        lblPageTitle.Location = new Point(22, 10);
        lblPageTitle.AutoSize = true;

        lblPageSubtitle.Text = "Environment overview and service controller";
        lblPageSubtitle.Font = new Font("Segoe UI", 8.25f);
        lblPageSubtitle.ForeColor = ModernColors.TextSecondary;
        lblPageSubtitle.Location = new Point(23, 33);
        lblPageSubtitle.AutoSize = true;
        lblPageSubtitle.AutoEllipsis = true;

        // Action Buttons
        btnOpenWww.Text = "Web Root";
        btnOpenWww.IconKind = IconKind.Folder;
        btnOpenWww.IconSize = 10;
        btnOpenWww.Width = 82;
        btnOpenWww.Height = 30;
        btnOpenWww.BorderRadius = 6;
        btnOpenWww.ShowBorder = true;
        btnOpenWww.NormalColor = ModernColors.Card;
        btnOpenWww.HoverColor = ModernColors.SurfaceHover;
        btnOpenWww.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnOpenWww.Click += BtnOpenWww_Click;

        btnOpenTerminal.Text = "Terminal";
        btnOpenTerminal.IconKind = IconKind.Terminal;
        btnOpenTerminal.IconSize = 10;
        btnOpenTerminal.Width = 78;
        btnOpenTerminal.Height = 30;
        btnOpenTerminal.BorderRadius = 6;
        btnOpenTerminal.ShowBorder = true;
        btnOpenTerminal.NormalColor = ModernColors.Card;
        btnOpenTerminal.HoverColor = ModernColors.SurfaceHover;
        btnOpenTerminal.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnOpenTerminal.Click += BtnOpenTerminal_Click;

        btnStartAll.Text = "Start All";
        btnStartAll.IconKind = IconKind.Play;
        btnStartAll.IconSize = 10;
        btnStartAll.Width = 78;
        btnStartAll.Height = 30;
        btnStartAll.BorderRadius = 6;
        btnStartAll.ShowBorder = false;
        btnStartAll.NormalColor = ModernColors.Success;
        btnStartAll.HoverColor = ModernColors.SuccessHover;
        btnStartAll.PressedColor = ModernColors.SuccessBg;
        btnStartAll.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnStartAll.Click += BtnStartAll_Click;

        btnStopAll.Text = "Stop All";
        btnStopAll.IconKind = IconKind.Stop;
        btnStopAll.IconSize = 10;
        btnStopAll.Width = 78;
        btnStopAll.Height = 30;
        btnStopAll.BorderRadius = 6;
        btnStopAll.ShowBorder = true;
        btnStopAll.BorderLineColor = Color.FromArgb(244, 63, 94, 140);
        btnStopAll.NormalColor = ModernColors.Card;
        btnStopAll.HoverColor = Color.FromArgb(45, 20, 28);
        btnStopAll.PressedColor = ModernColors.DangerBg;
        btnStopAll.ForeColor = Color.FromArgb(254, 205, 211);
        btnStopAll.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnStopAll.Click += BtnStopAll_Click;

        btnExit.Text = "Exit";
        btnExit.IconKind = IconKind.Power;
        btnExit.IconSize = 11;
        btnExit.Width = 62;
        btnExit.Height = 30;
        btnExit.BorderRadius = 6;
        btnExit.ShowBorder = true;
        btnExit.BorderLineColor = ModernColors.BorderSubtle;
        btnExit.NormalColor = ModernColors.Card;
        btnExit.HoverColor = Color.FromArgb(40, 20, 30);
        btnExit.PressedColor = ModernColors.DangerBg;
        btnExit.ForeColor = ModernColors.TextSecondary;
        btnExit.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnExit.Click += BtnExit_Click;

        topRibbon.Controls.Add(lblPageTitle);
        topRibbon.Controls.Add(lblPageSubtitle);
        topRibbon.Controls.AddRange([btnOpenWww, btnOpenTerminal, btnStartAll, btnStopAll, btnExit]);

        // Page Container
        pageContainer.Dock = DockStyle.Fill;
        pageContainer.BackColor = ModernColors.Background;

        // Bottom Footer
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Height = 30;
        footerPanel.BackColor = ModernColors.Surface;
        footerPanel.Padding = new Padding(20, 6, 20, 6);
        footerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
        };
        footerPanel.Resize += (s, e) =>
        {
            lblStatsText.Location = new Point(footerPanel.ClientSize.Width - lblStatsText.Width - 20, 7);
            int maxStatusW = lblStatsText.Left - lblStatusText.Left - 12;
            if (maxStatusW > 50)
            {
                lblStatusText.MaximumSize = new Size(maxStatusW, 18);
                lblStatusText.AutoEllipsis = true;
            }
        };

        lblStatusText.Text = "Ready - Dev Lite Server Portable Environment";
        lblStatusText.ForeColor = ModernColors.TextSecondary;
        lblStatusText.Font = new Font("Segoe UI", 8f);
        lblStatusText.Location = new Point(20, 7);
        lblStatusText.AutoSize = true;

        lblStatsText.Text = "0 / 0 active";
        lblStatsText.ForeColor = ModernColors.TextMuted;
        lblStatsText.Font = new Font("Segoe UI", 8f);
        lblStatsText.TextAlign = ContentAlignment.MiddleRight;
        lblStatsText.AutoSize = true;
        lblStatsText.Location = new Point(560, 7);

        footerPanel.Controls.Add(lblStatusText);
        footerPanel.Controls.Add(lblStatsText);

        mainPane.Controls.Add(pageContainer);
        mainPane.Controls.Add(topRibbon);
        mainPane.Controls.Add(footerPanel);

        // ==========================================
        // 3. MAIN FORM SHELL
        // ==========================================
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = ModernColors.Background;
        ClientSize = new Size(980, 620);
        MinimumSize = new Size(900, 560);
        Text = "Dev Lite Server - Portable Web Environment";
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(mainPane);
        Controls.Add(sidebarPanel);

        // Tray Icon
        notifyIcon.Text = "Dev Lite Server";
        notifyIcon.Visible = true;
        notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
    }

    private void LayoutTopRibbonButtons()
    {
        const int rightMargin = 18;
        const int spacing = 6;
        int w = topRibbon.ClientSize.Width > 200 ? topRibbon.ClientSize.Width : 785;
        const int top = 15;

        btnExit.Location = new Point(w - btnExit.Width - rightMargin, top);
        btnStopAll.Location = new Point(btnExit.Left - btnStopAll.Width - spacing, top);
        btnStartAll.Location = new Point(btnStopAll.Left - btnStartAll.Width - spacing, top);
        btnOpenTerminal.Location = new Point(btnStartAll.Left - btnOpenTerminal.Width - spacing - 8, top);
        btnOpenWww.Location = new Point(btnOpenTerminal.Left - btnOpenWww.Width - spacing, top);

        // Prevent title & subtitle from ever colliding with right action buttons
        int maxTitleWidth = btnOpenWww.Left - lblPageTitle.Left - 16;
        if (maxTitleWidth > 50)
        {
            lblPageTitle.MaximumSize = new Size(maxTitleWidth, 24);
            lblPageTitle.AutoEllipsis = true;
        }

        int maxSubWidth = btnOpenWww.Left - lblPageSubtitle.Left - 16;
        if (maxSubWidth > 50)
        {
            lblPageSubtitle.MaximumSize = new Size(maxSubWidth, 20);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreatePillPath(RectangleF rect)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float radius = rect.Height / 2f;
        float diameter = radius * 2f;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 90, 180);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 180);
        path.CloseFigure();
        return path;
    }
}
