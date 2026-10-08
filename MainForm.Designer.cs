using LiteServer.UI;

namespace LiteServer;

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

    private System.Windows.Forms.Panel actionsPanel;
    private ModernButton btnOpenWww;
    private ModernButton btnOpenTerminal;
    private ModernButton btnOpenMailpit;
    private ModernButton btnOpenAdminer;

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

        actionsPanel = new System.Windows.Forms.Panel();
        btnOpenWww = new ModernButton();
        btnOpenTerminal = new ModernButton();
        btnOpenMailpit = new ModernButton();
        btnOpenAdminer = new ModernButton();

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

        lblBrandTitle.Text = "⚡ Local Lite Server";
        lblBrandTitle.ForeColor = ModernColors.Primary;
        lblBrandTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
        lblBrandTitle.AutoSize = true;
        lblBrandTitle.Location = new Point(18, 12);

        lblBrandSub.Text = "Portable Local Web Environment";
        lblBrandSub.ForeColor = ModernColors.TextSecondary;
        lblBrandSub.Font = new Font("Segoe UI", 8.5f);
        lblBrandSub.AutoSize = true;
        lblBrandSub.Location = new Point(22, 38);

        btnStartAll.Text = "▶ Start All";
        btnStartAll.Width = 100;
        btnStartAll.Height = 36;
        btnStartAll.NormalColor = ModernColors.Success;
        btnStartAll.HoverColor = ModernColors.SuccessHover;
        btnStartAll.PressedColor = ModernColors.SuccessBg;
        btnStartAll.BorderRadius = 6;
        btnStartAll.ShowBorder = false;
        btnStartAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnStartAll.Location = new Point(480, 14);
        btnStartAll.Click += BtnStartAll_Click;

        btnStopAll.Text = "⏹ Stop All";
        btnStopAll.Width = 100;
        btnStopAll.Height = 36;
        btnStopAll.NormalColor = ModernColors.Danger;
        btnStopAll.HoverColor = ModernColors.DangerHover;
        btnStopAll.PressedColor = ModernColors.DangerBg;
        btnStopAll.BorderRadius = 6;
        btnStopAll.ShowBorder = false;
        btnStopAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnStopAll.Location = new Point(590, 14);
        btnStopAll.Click += BtnStopAll_Click;

        headerPanel.Controls.Add(lblBrandTitle);
        headerPanel.Controls.Add(lblBrandSub);
        headerPanel.Controls.Add(btnStartAll);
        headerPanel.Controls.Add(btnStopAll);

        //
        // actionsPanel (Secondary Toolbar)
        //
        actionsPanel.Dock = DockStyle.Top;
        actionsPanel.Height = 48;
        actionsPanel.BackColor = ModernColors.Background;
        actionsPanel.Padding = new Padding(20, 8, 20, 8);

        btnOpenWww.Text = "📁 Root (/www)";
        btnOpenWww.Width = 120;
        btnOpenWww.Height = 32;
        btnOpenWww.Location = new Point(20, 8);
        btnOpenWww.BorderRadius = 6;
        btnOpenWww.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenWww.Click += BtnOpenWww_Click;

        btnOpenTerminal.Text = "💻 Terminal";
        btnOpenTerminal.Width = 105;
        btnOpenTerminal.Height = 32;
        btnOpenTerminal.Location = new Point(150, 8);
        btnOpenTerminal.BorderRadius = 6;
        btnOpenTerminal.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenTerminal.Click += BtnOpenTerminal_Click;

        btnOpenMailpit.Text = "✉ Mailpit (:8025)";
        btnOpenMailpit.Width = 130;
        btnOpenMailpit.Height = 32;
        btnOpenMailpit.Location = new Point(265, 8);
        btnOpenMailpit.BorderRadius = 6;
        btnOpenMailpit.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenMailpit.Click += BtnOpenMailpit_Click;

        btnOpenAdminer.Text = "🗄 Adminer DB";
        btnOpenAdminer.Width = 120;
        btnOpenAdminer.Height = 32;
        btnOpenAdminer.Location = new Point(405, 8);
        btnOpenAdminer.BorderRadius = 6;
        btnOpenAdminer.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        btnOpenAdminer.Click += BtnOpenAdminer_Click;

        actionsPanel.Controls.AddRange([btnOpenWww, btnOpenTerminal, btnOpenMailpit, btnOpenAdminer]);

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

        lblStatusText.Text = "Ready - Local Lite Server Portable Environment";
        lblStatusText.ForeColor = ModernColors.TextSecondary;
        lblStatusText.Font = new Font("Segoe UI", 8f);
        lblStatusText.Dock = DockStyle.Fill;

        footerPanel.Controls.Add(lblStatusText);

        //
        // notifyIcon
        //
        notifyIcon.Text = "Local Lite Server";
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
        Text = "Local Lite Server - Portable Web Environment";
        StartPosition = FormStartPosition.CenterScreen;

        Controls.Add(cardContainer);
        Controls.Add(actionsPanel);
        Controls.Add(headerPanel);
        Controls.Add(footerPanel);

        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
    }
}
