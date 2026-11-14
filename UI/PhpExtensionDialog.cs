using System.Diagnostics;
using System.Drawing.Drawing2D;
using DevLiteServer.Core;
using DevLiteServer.Services;

namespace DevLiteServer.UI;

public class PhpExtensionDialog : Form
{
    private readonly string _phpDir;
    private readonly string _versionName;
    private readonly PhpService? _phpService;

    private readonly Panel _pnlList;
    private readonly ModernTextBox _txtSearch;
    private readonly Label _lblCount;
    private readonly ModernButton _btnRestart;
    private readonly ModernButton _btnClose;

    private List<PhpExtensionInfo> _allExtensions = [];
    public bool HasChanges { get; private set; }

    public PhpExtensionDialog(string phpDir, string versionName, PhpService? phpService = null)
    {
        _phpDir = phpDir;
        _versionName = versionName;
        _phpService = phpService;

        Text = $"PHP Extensions - {versionName}";
        ClientSize = new Size(640, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;

        // 1. Header Panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = ModernColors.Surface,
            Padding = new Padding(22, 12, 22, 10)
        };
        headerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "PHP Extensions Manager",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 12)
        };

        var lblSubtitle = new Label
        {
            Text = $"Runtime: {_versionName}  •  Toggle extensions On/Off to auto-update php.ini",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(20, 38)
        };

        var btnOpenIni = new ModernButton
        {
            Text = "php.ini",
            IconKind = IconKind.Code,
            IconSize = 11,
            Width = 78,
            Height = 28,
            BorderRadius = 6,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ShowBorder = true,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(ClientSize.Width - 190, 22)
        };
        btnOpenIni.Click += (s, e) =>
        {
            string phpIni = Path.Combine(_phpDir, "php.ini");
            if (File.Exists(phpIni))
            {
                Process.Start(new ProcessStartInfo(phpIni) { UseShellExecute = true });
            }
        };

        var btnOpenExtDir = new ModernButton
        {
            Text = "ext/ Folder",
            IconKind = IconKind.Folder,
            IconSize = 11,
            Width = 95,
            Height = 28,
            BorderRadius = 6,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ShowBorder = true,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Location = new Point(ClientSize.Width - 105, 22)
        };
        btnOpenExtDir.Click += (s, e) =>
        {
            string extDir = Path.Combine(_phpDir, "ext");
            if (Directory.Exists(extDir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", extDir) { UseShellExecute = true });
            }
        };

        headerPanel.Controls.AddRange([lblTitle, lblSubtitle, btnOpenIni, btnOpenExtDir]);

        // 2. Search Toolbar Panel
        var pnlSearch = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = ModernColors.Surface,
            Padding = new Padding(22, 8, 22, 8)
        };
        pnlSearch.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, pnlSearch.Height - 1, pnlSearch.Width, pnlSearch.Height - 1);
        };

        _txtSearch = new ModernTextBox
        {
            PlaceholderText = "Search extensions by name or function (e.g. curl, pdo, gd, intl)...",
            Dock = DockStyle.Fill,
            BorderRadius = 6,
            Font = new Font("Segoe UI", 9f)
        };
        _txtSearch.TextChanged += (s, e) => FilterList(_txtSearch.Text);

        pnlSearch.Controls.Add(_txtSearch);

        // 3. Footer Panel
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = ModernColors.Surface,
            Padding = new Padding(22, 10, 22, 10)
        };
        footerPanel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
        };

        _lblCount = new Label
        {
            Text = "Loading...",
            Font = new Font("Segoe UI", 8.75f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(22, 18)
        };

        _btnRestart = new ModernButton
        {
            Text = "Apply & Restart Workers",
            IconKind = IconKind.Play,
            IconSize = 11,
            Width = 175,
            Height = 34,
            Location = new Point(ClientSize.Width - 285, 11),
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.Primary,
            ShowBorder = true,
            BorderLineColor = Color.FromArgb(120, ModernColors.Primary),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        _btnRestart.Click += async (s, e) =>
        {
            if (_phpService != null && _phpService.Status == ServiceStatus.Running)
            {
                _btnRestart.Enabled = false;
                _btnRestart.Text = "Restarting...";
                await _phpService.RestartAsync();
                _btnRestart.Text = "Workers Restarted!";
                _btnRestart.ForeColor = ModernColors.Success;
                await Task.Delay(1500);
                _btnRestart.Text = "Apply & Restart Workers";
                _btnRestart.ForeColor = ModernColors.Primary;
                _btnRestart.Enabled = true;
            }
            else
            {
                MessageBox.Show(this, "PHP FastCGI is not currently running. Changes are saved to php.ini and will apply on next start.", "Extensions Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        _btnClose = new ModernButton
        {
            Text = "Done",
            Width = 85,
            Height = 34,
            Location = new Point(ClientSize.Width - 100, 11),
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            ForeColor = Color.White,
            ShowBorder = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _btnClose.Click += (s, e) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        footerPanel.Controls.AddRange([_lblCount, _btnRestart, _btnClose]);

        // 4. Scrollable Container Panel
        _pnlList = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(22, 12, 22, 12),
            BackColor = ModernColors.Background
        };

        Controls.Add(_pnlList);
        Controls.Add(footerPanel);
        Controls.Add(pnlSearch);
        Controls.Add(headerPanel);

        LoadExtensions();
    }

    private void LoadExtensions()
    {
        _allExtensions = PhpExtensionManager.GetExtensions(_phpDir);
        FilterList(_txtSearch.Text);
    }

    private void FilterList(string query)
    {
        _pnlList.SuspendLayout();
        _pnlList.Controls.Clear();

        var q = query.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q)
            ? _allExtensions
            : _allExtensions.Where(e => e.Name.ToLowerInvariant().Contains(q) || e.Description.ToLowerInvariant().Contains(q)).ToList();

        int enabledCount = _allExtensions.Count(e => e.IsEnabled);
        _lblCount.Text = $"{enabledCount} of {_allExtensions.Count} extensions enabled";

        if (filtered.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "No matching extensions found.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = ModernColors.TextMuted,
                AutoSize = true,
                Location = new Point(24, 20)
            };
            _pnlList.Controls.Add(lblEmpty);
            _pnlList.ResumeLayout();
            return;
        }

        int top = 8;
        int cardW = _pnlList.ClientSize.Width - 44;

        foreach (var ext in filtered)
        {
            var rowCard = CreateExtensionRow(ext, cardW);
            rowCard.Location = new Point(22, top);
            rowCard.Width = cardW;
            rowCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _pnlList.Controls.Add(rowCard);
            top += rowCard.Height + 8;
        }

        _pnlList.ResumeLayout();
    }

    private Panel CreateExtensionRow(PhpExtensionInfo ext, int width)
    {
        var row = new Panel
        {
            Height = 54,
            BackColor = ModernColors.Card,
            Padding = new Padding(14, 8, 14, 8)
        };

        row.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(0.5f, 0.5f, row.Width - 1f, row.Height - 1f);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var bgBrush = new SolidBrush(ModernColors.Card);
            g.FillPath(bgBrush, path);

            using var borderPen = new Pen(ext.IsEnabled ? Color.FromArgb(100, ModernColors.Primary) : ModernColors.BorderSubtle, 1f);
            g.DrawPath(borderPen, path);
        };

        // Name
        var lblName = new Label
        {
            Text = ext.Name,
            Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
            ForeColor = ext.IsEnabled ? ModernColors.TextPrimary : ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(14, 9)
        };

        // Zend Tag
        Label? lblZend = null;
        if (ext.IsZend)
        {
            lblZend = new Label
            {
                Text = "ZEND",
                Font = new Font("Segoe UI", 6.5f, FontStyle.Bold),
                ForeColor = ModernColors.Primary,
                BackColor = ModernColors.PrimaryBg,
                AutoSize = true,
                Padding = new Padding(4, 1, 4, 1),
                Location = new Point(14 + TextRenderer.MeasureText(ext.Name, lblName.Font).Width + 8, 11)
            };
        }

        // Description
        var lblDesc = new Label
        {
            Text = ext.Description,
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextMuted,
            AutoSize = true,
            AutoEllipsis = true,
            MaximumSize = new Size(width - 120, 18),
            Location = new Point(14, 30)
        };

        // Toggle Switch
        var toggle = new ModernToggle
        {
            Checked = ext.IsEnabled,
            Width = 44,
            Height = 22,
            Location = new Point(row.Width - 58, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        toggle.CheckedChanged += (s, e) =>
        {
            bool ok = PhpExtensionManager.SetExtensionEnabled(_phpDir, ext.Name, toggle.Checked);
            if (ok)
            {
                ext.IsEnabled = toggle.Checked;
                HasChanges = true;
                lblName.ForeColor = ext.IsEnabled ? ModernColors.TextPrimary : ModernColors.TextSecondary;
                row.Invalidate();
                int enabledCount = _allExtensions.Count(x => x.IsEnabled);
                _lblCount.Text = $"{enabledCount} of {_allExtensions.Count} extensions enabled";
            }
            else
            {
                toggle.Checked = !toggle.Checked;
                MessageBox.Show(this, "Failed to update php.ini.", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        row.Controls.Add(lblName);
        if (lblZend != null) row.Controls.Add(lblZend);
        row.Controls.Add(lblDesc);
        row.Controls.Add(toggle);

        return row;
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
}
