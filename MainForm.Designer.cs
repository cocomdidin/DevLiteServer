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
    private ModernButton btnCheckUpdates;

    private System.Windows.Forms.Panel cardContainer;
    private System.Windows.Forms.Panel footerPanel;
    private System.Windows.Forms.Label lblStatusText;

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
        btnCheckUpdates = new ModernButton();

        cardContainer = new System.Windows.Forms.Panel();
        footerPanel = new System.Windows.Forms.Panel();
        lblStatusText = new System.Windows.Forms.Label();

        SuspendLayout();

        //
        // headerPanel
        //
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Height = 65;
        headerPanel.BackColor = ModernColors.Surface;
        headerPanel.Padding = new Padding(20, 12, 20, 12);
        headerPanel.Resize += (s, e) =>
        {
            const int rightMargin = 20;
            const int spacing = 8;
            btnExit.Location = new Point(headerPanel.ClientSize.Width - btnExit.Width - rightMargin, 15);
            btnStopAll.Location = new Point(btnExit.Left - btnStopAll.Width - spacing, 15);
            btnStartAll.Location = new Point(btnStopAll.Left - btnStartAll.Width - spacing, 15);
        };
        headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Brand Icon Circle Badge
            var badgeRect = new RectangleF(20, 18, 26, 26);
            using var badgeBg = new SolidBrush(Color.FromArgb(24, 38, 60));
            using var badgeBorder = new Pen(Color.FromArgb(56, 189, 248, 120), 1);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(25, 23, 16, 16);
            VectorIcons.Draw(g, IconKind.Lightning, iconRect, ModernColors.Primary);

            // Bottom border line
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        lblBrandTitle.Text = "Dev Lite Server";
        lblBrandTitle.ForeColor = ModernColors.TextPrimary;
        lblBrandTitle.Font = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        lblBrandTitle.AutoSize = true;
        lblBrandTitle.Location = new Point(56, 12);

        lblBrandSub.Text = "Portable Local Web Environment (WEMP Stack)";
        lblBrandSub.ForeColor = ModernColors.TextSecondary;
        lblBrandSub.Font = new Font("Segoe UI", 8.25f);
        lblBrandSub.AutoSize = true;
        lblBrandSub.Location = new Point(58, 36);

        btnStartAll.Text = "Start All";
        btnStartAll.IconKind = IconKind.Play;
        btnStartAll.IconSize = 10;
        btnStartAll.Width = 92;
        btnStartAll.Height = 34;
        btnStartAll.NormalColor = ModernColors.Success;
        btnStartAll.HoverColor = ModernColors.SuccessHover;
        btnStartAll.PressedColor = ModernColors.SuccessBg;
        btnStartAll.BorderRadius = 6;
        btnStartAll.ShowBorder = false;
        btnStartAll.ForeColor = Color.White;
        btnStartAll.Location = new Point(406, 15);
        btnStartAll.Click += BtnStartAll_Click;

        btnStopAll.Text = "Stop All";
        btnStopAll.IconKind = IconKind.Stop;
        btnStopAll.IconSize = 10;
        btnStopAll.Width = 92;
        btnStopAll.Height = 34;
        btnStopAll.NormalColor = Color.FromArgb(45, 30, 15);
        btnStopAll.BorderLineColor = ModernColors.Warning;
        btnStopAll.HoverColor = ModernColors.Warning;
        btnStopAll.PressedColor = ModernColors.WarningBg;
        btnStopAll.BorderRadius = 6;
        btnStopAll.ShowBorder = true;
        btnStopAll.ForeColor = Color.FromArgb(254, 240, 138);
        btnStopAll.CustomIconColor = ModernColors.Warning;
        btnStopAll.Location = new Point(506, 15);
        btnStopAll.Click += BtnStopAll_Click;

        btnExit.Text = "Exit";
        btnExit.IconKind = IconKind.Power;
        btnExit.IconSize = 12;
        btnExit.Width = 84;
        btnExit.Height = 34;
        btnExit.NormalColor = Color.FromArgb(45, 18, 25);
        btnExit.BorderLineColor = ModernColors.Danger;
        btnExit.HoverColor = ModernColors.Danger;
        btnExit.PressedColor = ModernColors.DangerBg;
        btnExit.BorderRadius = 6;
        btnExit.BorderLineColor = ModernColors.Danger;
        btnExit.ShowBorder = true;
        btnExit.ForeColor = Color.FromArgb(254, 205, 211);
        btnExit.CustomIconColor = ModernColors.Danger;
        btnExit.Location = new Point(606, 15);
        btnExit.Click += BtnExit_Click;

        headerPanel.Controls.Add(lblBrandTitle);
        headerPanel.Controls.Add(lblBrandSub);
        headerPanel.Controls.Add(btnStartAll);
        headerPanel.Controls.Add(btnStopAll);
        headerPanel.Controls.Add(btnExit);

        //
        // actionsPanel (Secondary Toolbar)
        //
        actionsPanel.Dock = DockStyle.Top;
        actionsPanel.Height = 48;
        actionsPanel.BackColor = ModernColors.Background;
        actionsPanel.Padding = new Padding(20, 8, 20, 8);
        actionsPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, actionsPanel.Height - 1, actionsPanel.Width, actionsPanel.Height - 1);
        };

        btnOpenWww.Text = "Root (/www)";
        btnOpenWww.IconKind = IconKind.Folder;
        btnOpenWww.IconSize = 12;
        btnOpenWww.Width = 114;
        btnOpenWww.Height = 32;
        btnOpenWww.Location = new Point(20, 8);
        btnOpenWww.BorderRadius = 6;
        btnOpenWww.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenWww.Click += BtnOpenWww_Click;

        btnOpenTerminal.Text = "Terminal";
        btnOpenTerminal.IconKind = IconKind.Terminal;
        btnOpenTerminal.IconSize = 12;
        btnOpenTerminal.Width = 104;
        btnOpenTerminal.Height = 32;
        btnOpenTerminal.Location = new Point(142, 8);
        btnOpenTerminal.BorderRadius = 6;
        btnOpenTerminal.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenTerminal.Click += BtnOpenTerminal_Click;

        btnOpenMailpit.Text = "Mailpit (:8025)";
        btnOpenMailpit.IconKind = IconKind.Mail;
        btnOpenMailpit.IconSize = 12;
        btnOpenMailpit.Width = 124;
        btnOpenMailpit.Height = 32;
        btnOpenMailpit.Location = new Point(254, 8);
        btnOpenMailpit.BorderRadius = 6;
        btnOpenMailpit.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenMailpit.Click += BtnOpenMailpit_Click;

        btnOpenAdminer.Text = "Adminer DB";
        btnOpenAdminer.IconKind = IconKind.Database;
        btnOpenAdminer.IconSize = 12;
        btnOpenAdminer.Width = 116;
        btnOpenAdminer.Height = 32;
        btnOpenAdminer.Location = new Point(386, 8);
        btnOpenAdminer.BorderRadius = 6;
        btnOpenAdminer.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenAdminer.Click += BtnOpenAdminer_Click;

        btnCheckUpdates.Text = "Updates";
        btnCheckUpdates.IconKind = IconKind.Refresh;
        btnCheckUpdates.IconSize = 12;
        btnCheckUpdates.Width = 96;
        btnCheckUpdates.Height = 32;
        btnCheckUpdates.Location = new Point(510, 8);
        btnCheckUpdates.BorderRadius = 6;
        btnCheckUpdates.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnCheckUpdates.Click += BtnCheckUpdates_Click;

        actionsPanel.Controls.AddRange([btnOpenWww, btnOpenTerminal, btnOpenMailpit, btnOpenAdminer, btnCheckUpdates]);

        //
        // cardContainer
        //
        cardContainer.Dock = DockStyle.Fill;
        cardContainer.BackColor = ModernColors.Background;
        cardContainer.Padding = new Padding(20, 6, 20, 10);
        cardContainer.AutoScroll = true;

        //
        // footerPanel
        //
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Height = 28;
        footerPanel.BackColor = ModernColors.Surface;
        footerPanel.Padding = new Padding(16, 4, 16, 4);

        lblStatusText.Text = "Ready - Dev Lite Server Portable Environment";
        lblStatusText.ForeColor = ModernColors.TextSecondary;
        lblStatusText.Font = new Font("Segoe UI", 8f);
        lblStatusText.Dock = DockStyle.Fill;

        footerPanel.Controls.Add(lblStatusText);

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
        ClientSize = new Size(710, 470);
        MinimumSize = new Size(710, 420);
        Text = "Dev Lite Server - Portable Web Environment";
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(cardContainer);
        Controls.Add(actionsPanel);
        Controls.Add(headerPanel);
        Controls.Add(footerPanel);

        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
    }
}
