using DevLiteServer.UI;

namespace DevLiteServer;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private System.Windows.Forms.NotifyIcon notifyIcon;
    private System.Windows.Forms.ContextMenuStrip trayMenu;

    private System.Windows.Forms.Panel headerPanel;
    private System.Windows.Forms.Label lblBrandTitle;
    private System.Windows.Forms.Label lblBrandSub;
    private ModernButton btnStartAll;
    private ModernButton btnStopAll;
    private ModernButton btnExit;

    private System.Windows.Forms.Panel actionsPanel;
    private ModernButton btnOpenWww;
    private ModernButton btnOpenTerminal;
    private ModernButton btnOpenMailpit;
    private ModernButton btnOpenAdminer;
    private ModernButton btnSettings;
    private ModernButton btnCheckUpdates;

    private System.Windows.Forms.Panel cardContainer;
    private System.Windows.Forms.Panel footerPanel;
    private System.Windows.Forms.Label lblStatusText;
    private System.Windows.Forms.Label lblStatsText;

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

        headerPanel = new System.Windows.Forms.Panel();
        lblBrandTitle = new System.Windows.Forms.Label();
        lblBrandSub = new System.Windows.Forms.Label();
        btnStartAll = new ModernButton();
        btnStopAll = new ModernButton();
        btnExit = new ModernButton();

        actionsPanel = new System.Windows.Forms.Panel();
        btnOpenWww = new ModernButton();
        btnOpenTerminal = new ModernButton();
        btnOpenMailpit = new ModernButton();
        btnOpenAdminer = new ModernButton();
        btnSettings = new ModernButton();
        btnCheckUpdates = new ModernButton();

        cardContainer = new System.Windows.Forms.Panel();
        footerPanel = new System.Windows.Forms.Panel();
        lblStatusText = new System.Windows.Forms.Label();
        lblStatsText = new System.Windows.Forms.Label();

        SuspendLayout();

        //
        // headerPanel
        //
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Height = 68;
        headerPanel.BackColor = ModernColors.Surface;
        headerPanel.Padding = new Padding(20, 12, 20, 12);
        headerPanel.Resize += (s, e) =>
        {
            const int rightMargin = 20;
            const int spacing = 8;
            int w = headerPanel.ClientSize.Width > 200 ? headerPanel.ClientSize.Width : (ClientSize.Width > 200 ? ClientSize.Width : 760);
            btnExit.Location = new Point(w - btnExit.Width - rightMargin, 17);
            btnStopAll.Location = new Point(btnExit.Left - btnStopAll.Width - spacing, 17);
            btnStartAll.Location = new Point(btnStopAll.Left - btnStartAll.Width - spacing, 17);
        };
        headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Brand Icon Circle Badge (36x36)
            var badgeRect = new RectangleF(20, 16, 36, 36);
            using var badgeBg = new SolidBrush(Color.FromArgb(20, 32, 52));
            using var badgeBorder = new Pen(Color.FromArgb(56, 189, 248, 140), 1.25f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(28, 24, 20, 20);
            VectorIcons.Draw(g, IconKind.Lightning, iconRect, ModernColors.Primary);

            // "PORTABLE" subtle badge next to title
            var tagRect = new RectangleF(208, 17, 62, 17);
            using var tagBg = new SolidBrush(Color.FromArgb(24, 38, 60));
            using var tagPen = new Pen(Color.FromArgb(56, 189, 248, 80), 1f);
            using var tagPath = CreatePillPath(tagRect);
            g.FillPath(tagBg, tagPath);
            g.DrawPath(tagPen, tagPath);

            using var tagFont = new Font("Segoe UI", 7f, FontStyle.Bold);
            TextRenderer.DrawText(g, "PORTABLE", tagFont, Rectangle.Round(tagRect), ModernColors.Primary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // Bottom border line
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        lblBrandTitle.Text = "Dev Lite Server";
        lblBrandTitle.ForeColor = ModernColors.TextPrimary;
        lblBrandTitle.Font = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        lblBrandTitle.AutoSize = true;
        lblBrandTitle.Location = new Point(66, 14);

        lblBrandSub.Text = "High-Performance Portable Local WEMP & Database Stack";
        lblBrandSub.ForeColor = ModernColors.TextSecondary;
        lblBrandSub.Font = new Font("Segoe UI", 8.25f);
        lblBrandSub.AutoSize = true;
        lblBrandSub.Location = new Point(67, 38);

        btnStartAll.Text = "Start All";
        btnStartAll.IconKind = IconKind.Play;
        btnStartAll.IconSize = 10;
        btnStartAll.Width = 96;
        btnStartAll.Height = 34;
        btnStartAll.NormalColor = ModernColors.Success;
        btnStartAll.HoverColor = ModernColors.SuccessHover;
        btnStartAll.PressedColor = ModernColors.SuccessBg;
        btnStartAll.BorderRadius = 6;
        btnStartAll.ShowBorder = false;
        btnStartAll.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnStartAll.Click += BtnStartAll_Click;

        btnStopAll.Text = "Stop All";
        btnStopAll.IconKind = IconKind.Stop;
        btnStopAll.IconSize = 10;
        btnStopAll.Width = 96;
        btnStopAll.Height = 34;
        btnStopAll.NormalColor = ModernColors.Card;
        btnStopAll.HoverColor = Color.FromArgb(45, 20, 28);
        btnStopAll.PressedColor = ModernColors.DangerBg;
        btnStopAll.BorderRadius = 6;
        btnStopAll.ShowBorder = true;
        btnStopAll.BorderLineColor = Color.FromArgb(244, 63, 94, 160);
        btnStopAll.ForeColor = Color.FromArgb(254, 205, 211);
        btnStopAll.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnStopAll.Click += BtnStopAll_Click;

        btnExit.Text = "Exit";
        btnExit.IconKind = IconKind.Power;
        btnExit.IconSize = 11;
        btnExit.Width = 76;
        btnExit.Height = 34;
        btnExit.NormalColor = ModernColors.Card;
        btnExit.HoverColor = Color.FromArgb(40, 20, 30);
        btnExit.PressedColor = ModernColors.DangerBg;
        btnExit.BorderRadius = 6;
        btnExit.ShowBorder = true;
        btnExit.BorderLineColor = ModernColors.BorderSubtle;
        btnExit.ForeColor = ModernColors.TextSecondary;
        btnExit.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnExit.Click += BtnExit_Click;

        headerPanel.Controls.Add(lblBrandTitle);
        headerPanel.Controls.Add(lblBrandSub);
        headerPanel.Controls.Add(btnStartAll);
        headerPanel.Controls.Add(btnStopAll);
        headerPanel.Controls.Add(btnExit);

        //
        // actionsPanel (Toolbar)
        //
        actionsPanel.Dock = DockStyle.Top;
        actionsPanel.Height = 46;
        actionsPanel.BackColor = Color.FromArgb(13, 19, 31);
        actionsPanel.Padding = new Padding(20, 7, 20, 7);
        actionsPanel.Resize += (s, e) => LayoutActionButtons();
        actionsPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, actionsPanel.Height - 1, actionsPanel.Width, actionsPanel.Height - 1);
        };

        btnOpenWww.Text = "Web Root";
        btnOpenWww.IconKind = IconKind.Folder;
        btnOpenWww.IconSize = 11;
        btnOpenWww.Width = 98;
        btnOpenWww.Height = 31;
        btnOpenWww.BorderRadius = 6;
        btnOpenWww.ShowBorder = true;
        btnOpenWww.NormalColor = ModernColors.Surface;
        btnOpenWww.HoverColor = ModernColors.SurfaceHover;
        btnOpenWww.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnOpenWww.Click += BtnOpenWww_Click;

        btnOpenTerminal.Text = "Terminal";
        btnOpenTerminal.IconKind = IconKind.Terminal;
        btnOpenTerminal.IconSize = 11;
        btnOpenTerminal.Width = 92;
        btnOpenTerminal.Height = 31;
        btnOpenTerminal.BorderRadius = 6;
        btnOpenTerminal.ShowBorder = true;
        btnOpenTerminal.NormalColor = ModernColors.Surface;
        btnOpenTerminal.HoverColor = ModernColors.SurfaceHover;
        btnOpenTerminal.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnOpenTerminal.Click += BtnOpenTerminal_Click;

        btnOpenMailpit.Text = "Mailpit";
        btnOpenMailpit.IconKind = IconKind.Mail;
        btnOpenMailpit.IconSize = 11;
        btnOpenMailpit.Width = 86;
        btnOpenMailpit.Height = 31;
        btnOpenMailpit.BorderRadius = 6;
        btnOpenMailpit.ShowBorder = true;
        btnOpenMailpit.NormalColor = ModernColors.Surface;
        btnOpenMailpit.HoverColor = ModernColors.SurfaceHover;
        btnOpenMailpit.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnOpenMailpit.Click += BtnOpenMailpit_Click;

        btnOpenAdminer.Text = "Adminer DB";
        btnOpenAdminer.IconKind = IconKind.Database;
        btnOpenAdminer.IconSize = 11;
        btnOpenAdminer.Width = 104;
        btnOpenAdminer.Height = 31;
        btnOpenAdminer.BorderRadius = 6;
        btnOpenAdminer.ShowBorder = true;
        btnOpenAdminer.NormalColor = ModernColors.Surface;
        btnOpenAdminer.HoverColor = ModernColors.SurfaceHover;
        btnOpenAdminer.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnOpenAdminer.Click += BtnOpenAdminer_Click;

        btnSettings.Text = "Settings";
        btnSettings.IconKind = IconKind.Gear;
        btnSettings.IconSize = 11;
        btnSettings.Width = 90;
        btnSettings.Height = 31;
        btnSettings.BorderRadius = 6;
        btnSettings.ShowBorder = true;
        btnSettings.NormalColor = ModernColors.Surface;
        btnSettings.HoverColor = ModernColors.SurfaceHover;
        btnSettings.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnSettings.Click += BtnSettings_Click;

        btnCheckUpdates.Text = "Updates";
        btnCheckUpdates.IconKind = IconKind.Refresh;
        btnCheckUpdates.IconSize = 11;
        btnCheckUpdates.Width = 88;
        btnCheckUpdates.Height = 31;
        btnCheckUpdates.BorderRadius = 6;
        btnCheckUpdates.ShowBorder = true;
        btnCheckUpdates.NormalColor = ModernColors.Surface;
        btnCheckUpdates.HoverColor = ModernColors.SurfaceHover;
        btnCheckUpdates.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        btnCheckUpdates.Click += BtnCheckUpdates_Click;

        actionsPanel.Controls.AddRange([btnOpenWww, btnOpenTerminal, btnOpenMailpit, btnOpenAdminer, btnSettings, btnCheckUpdates]);

        //
        // cardContainer
        //
        cardContainer.Dock = DockStyle.Fill;
        cardContainer.BackColor = ModernColors.Background;
        cardContainer.Padding = new Padding(20, 10, 20, 10);
        cardContainer.AutoScroll = true;

        //
        // footerPanel
        //
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Height = 32;
        footerPanel.BackColor = ModernColors.Surface;
        footerPanel.Padding = new Padding(20, 6, 20, 6);
        footerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
        };
        footerPanel.Resize += (s, e) =>
        {
            lblStatsText.Location = new Point(footerPanel.ClientSize.Width - lblStatsText.Width - 20, 8);
        };

        lblStatusText.Text = "Ready - Dev Lite Server Portable Environment";
        lblStatusText.ForeColor = ModernColors.TextSecondary;
        lblStatusText.Font = new Font("Segoe UI", 8.25f);
        lblStatusText.Location = new Point(20, 8);
        lblStatusText.AutoSize = true;

        lblStatsText.Text = "0 / 0 active";
        lblStatsText.ForeColor = ModernColors.TextMuted;
        lblStatsText.Font = new Font("Segoe UI", 8.25f);
        lblStatsText.TextAlign = ContentAlignment.MiddleRight;
        lblStatsText.AutoSize = true;
        lblStatsText.Location = new Point(560, 8);

        footerPanel.Controls.Add(lblStatusText);
        footerPanel.Controls.Add(lblStatsText);

        //
        // notifyIcon
        //
        notifyIcon.Text = "Dev Lite Server";
        notifyIcon.Visible = true;
        notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = ModernColors.Background;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(740, 500);
        Text = "Dev Lite Server - Portable Web Environment";
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(cardContainer);
        Controls.Add(actionsPanel);
        Controls.Add(headerPanel);
        Controls.Add(footerPanel);

        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
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
