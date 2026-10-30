using System.Drawing.Drawing2D;
using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class DbClientDialog : Form
{
    private readonly string _serviceKey;
    private readonly string _appRoot;
    private readonly AppConfig _config;
    private readonly List<DbClientInfo> _clients;

    private DbClientInfo? _selectedClient;
    private readonly CheckBox _chkRemember;
    private readonly ModernButton _btnAction;
    private readonly ModernButton _btnCancel;
    private readonly Label _lblProgress;
    private readonly ProgressBar _progressBar;
    private readonly Panel _pnlCardContainer;
    private readonly List<Panel> _cardPanels = [];
    private CancellationTokenSource? _cts;

    public DbClientInfo? SelectedClient => _selectedClient;
    public bool RememberChoice => _chkRemember.Checked;

    public DbClientDialog(string serviceKey, string appRoot, AppConfig config)
    {
        _serviceKey = serviceKey;
        _appRoot = appRoot;
        _config = config;
        _clients = DatabaseClientManager.GetClientsForService(serviceKey, appRoot);

        Text = "Select Database Client";
        ClientSize = new Size(580, 530);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;

        // 1. Header
        string titleSuffix = serviceKey.ToLowerInvariant() switch
        {
            "mysql" => $"MySQL (Port {config.MysqlPort})",
            "postgresql" or "postgres" => $"PostgreSQL (Port {config.PostgreSqlPort})",
            "redis" => $"Redis (Port {config.RedisPort})",
            _ => serviceKey
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = ModernColors.Surface
        };
        headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var badgeRect = new RectangleF(20, 16, 38, 38);
            using var badgeBg = new SolidBrush(Color.FromArgb(12, 74, 110));
            using var badgeBorder = new Pen(Color.FromArgb(56, 189, 248, 120), 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(29, 25, 20, 20);
            VectorIcons.Draw(g, IconKind.Database, iconRect, ModernColors.Primary);

            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = $"Select Database Client • {titleSuffix}",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(68, 14),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Choose your preferred interface to inspect tables, run queries, and manage data.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(70, 38),
            AutoSize = true
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSub);

        // 2. Middle client cards container
        _pnlCardContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20, 12, 20, 10),
            BackColor = ModernColors.Background
        };

        // 3. Footer panel
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            BackColor = ModernColors.Surface,
            Padding = new Padding(20, 10, 20, 12)
        };
        footerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
        };

        _chkRemember = new CheckBox
        {
            Text = $"Always use this client as default for {serviceKey.ToUpperInvariant()}",
            Font = new Font("Segoe UI", 8.75f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(20, 10),
            AutoSize = true,
            Cursor = Cursors.Hand
        };

        _lblProgress = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.Primary,
            Location = new Point(20, 36),
            Size = new Size(320, 18),
            Visible = false
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 58),
            Size = new Size(320, 12),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Visible = false
        };

        _btnAction = new ModernButton
        {
            Text = "Launch Client",
            IconKind = IconKind.Play,
            IconSize = 12,
            Width = 135,
            Height = 36,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            ForeColor = Color.FromArgb(11, 15, 25),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(footerPanel.Width - 275, 46),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnAction.Click += async (s, e) => await OnActionClickAsync();

        _btnCancel = new ModernButton
        {
            Text = "Cancel",
            Width = 85,
            Height = 36,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(footerPanel.Width - 110, 46),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _btnCancel.Click += (s, e) =>
        {
            _cts?.Cancel();
            DialogResult = DialogResult.Cancel;
            Close();
        };

        footerPanel.Controls.Add(_chkRemember);
        footerPanel.Controls.Add(_lblProgress);
        footerPanel.Controls.Add(_progressBar);
        footerPanel.Controls.Add(_btnAction);
        footerPanel.Controls.Add(_btnCancel);

        Controls.Add(_pnlCardContainer);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);

        BuildClientCards();
    }

    private void BuildClientCards()
    {
        _pnlCardContainer.Controls.Clear();
        _cardPanels.Clear();

        int top = 10;
        foreach (var client in _clients)
        {
            var card = CreateClientCard(client, top);
            _pnlCardContainer.Controls.Add(card);
            _cardPanels.Add(card);
            top += card.Height + 10;
        }

        // Auto select first installed client or default to first
        var defaultCandidate = _clients.FirstOrDefault(c => c.IsInstalled) ?? _clients.FirstOrDefault();
        if (defaultCandidate != null)
        {
            SelectClient(defaultCandidate);
        }
    }

    private Panel CreateClientCard(DbClientInfo client, int top)
    {
        var card = new Panel
        {
            Location = new Point(15, top),
            Size = new Size(_pnlCardContainer.Width - 55, 78),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = ModernColors.Card,
            Cursor = Cursors.Hand,
            Tag = client
        };

        card.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isSelected = _selectedClient == client;
            var borderPenColor = isSelected ? ModernColors.Primary : ModernColors.BorderSubtle;
            int borderWidth = isSelected ? 2 : 1;

            using var p = new Pen(borderPenColor, borderWidth);
            var rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            g.DrawRectangle(p, rect);

            // Radio indicator
            var radioCenter = new PointF(22, card.Height / 2f);
            using var radioBorder = new Pen(isSelected ? ModernColors.Primary : ModernColors.BorderLight, 1.5f);
            g.DrawEllipse(radioBorder, radioCenter.X - 8, radioCenter.Y - 8, 16, 16);

            if (isSelected)
            {
                using var radioDot = new SolidBrush(ModernColors.Primary);
                g.FillEllipse(radioDot, radioCenter.X - 4, radioCenter.Y - 4, 8, 8);
            }
        };

        var lblName = new Label
        {
            Text = client.Name,
            Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(44, 10),
            AutoSize = true,
            Cursor = Cursors.Hand
        };

        var lblCategory = new Label
        {
            Text = client.Category,
            Font = new Font("Segoe UI", 8f),
            ForeColor = ModernColors.Primary,
            Location = new Point(lblName.Right + 10, 12),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        lblName.TextChanged += (s, e) => lblCategory.Location = new Point(lblName.Right + 10, 12);

        var lblBadge = new Label
        {
            Text = client.IsInstalled ? "INSTALLED" : $"DOWNLOAD ({client.ApproximateSize})",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = client.IsInstalled ? ModernColors.Success : ModernColors.TextSecondary,
            BackColor = client.IsInstalled ? Color.FromArgb(20, 83, 45) : Color.FromArgb(31, 41, 55),
            Padding = new Padding(6, 2, 6, 2),
            Location = new Point(card.Width - 145, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            Cursor = Cursors.Hand
        };

        var lblDesc = new Label
        {
            Text = client.Description,
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(44, 34),
            Size = new Size(card.Width - 60, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };

        card.Controls.Add(lblName);
        card.Controls.Add(lblCategory);
        card.Controls.Add(lblBadge);
        card.Controls.Add(lblDesc);

        // Click handlers on all children
        Action selectThis = () => SelectClient(client);
        card.Click += (s, e) => selectThis();
        lblName.Click += (s, e) => selectThis();
        lblCategory.Click += (s, e) => selectThis();
        lblBadge.Click += (s, e) => selectThis();
        lblDesc.Click += (s, e) => selectThis();

        return card;
    }

    private void SelectClient(DbClientInfo client)
    {
        _selectedClient = client;

        if (client.IsInstalled)
        {
            _btnAction.Text = "Launch Client";
            _btnAction.IconKind = IconKind.Play;
        }
        else
        {
            _btnAction.Text = "Install & Launch";
            _btnAction.IconKind = IconKind.Download;
        }

        foreach (var c in _cardPanels)
        {
            c.BackColor = (c.Tag == client) ? ModernColors.CardHover : ModernColors.Card;
            c.Invalidate();
        }
    }

    private async Task OnActionClickAsync()
    {
        if (_selectedClient == null) return;

        if (_selectedClient.IsInstalled)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        // Needs download & installation
        _btnAction.Enabled = false;
        _btnCancel.Enabled = false;
        _lblProgress.Visible = true;
        _progressBar.Visible = true;
        _lblProgress.Text = "Preparing download...";
        _lblProgress.ForeColor = ModernColors.Primary;

        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(msg =>
        {
            if (InvokeRequired)
            {
                Invoke(() => _lblProgress.Text = msg);
            }
            else
            {
                _lblProgress.Text = msg;
            }
        });

        bool success = await DatabaseClientManager.DownloadAndInstallClientAsync(
            _selectedClient.Type,
            _appRoot,
            _config,
            progress,
            _cts.Token);

        if (success)
        {
            _selectedClient.IsInstalled = true;
            _lblProgress.Text = "Installation complete!";
            _lblProgress.ForeColor = ModernColors.Success;
            await Task.Delay(400);

            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _btnAction.Enabled = true;
            _btnCancel.Enabled = true;
            _lblProgress.Text = "Installation failed. Please retry.";
            _lblProgress.ForeColor = ModernColors.Danger;
        }
    }
}
