using System.Diagnostics;
using DevLiteServer.Core;
using DevLiteServer.Services;

namespace DevLiteServer.UI;

public class SiteEditDialog : Form
{
    private readonly string _appRoot;
    private readonly AppConfig _config;
    private readonly SiteItem? _existingSite;

    private readonly TextBox _txtPath;
    private readonly TextBox _txtDomain;
    private readonly ComboBox _cmbDocRoot;
    private readonly ComboBox _cmbPhp;
    private readonly CheckBox _chkSsl;
    private readonly Label _lblError;

    public SiteItem? ResultSite { get; private set; }

    public SiteEditDialog(string appRoot, AppConfig config, SiteItem? existingSite = null)
    {
        _appRoot = appRoot;
        _config = config;
        _existingSite = existingSite;

        Text = existingSite == null ? "Add Site - Dev Lite Server" : "Edit Site - Dev Lite Server";
        ClientSize = new Size(520, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 9f);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = ModernColors.Surface,
            Padding = new Padding(22, 14, 22, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var p = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = existingSite == null ? "Add New Site (Local or External)" : $"Edit Site: {existingSite.Domain}",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 12)
        };

        var lblSub = new Label
        {
            Text = "Configure project location, virtual host domain, PHP runtime, and SSL.",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(21, 36)
        };
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSub);

        // Form Body
        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 12),
            AutoScroll = true
        };

        int top = 16;
        const int labelH = 18;
        const int fieldH = 28;
        const int spacing = 14;
        int fieldW = ClientSize.Width - 48;

        // 1. Project Folder
        var lblPath = new Label
        {
            Text = "Project Folder Path:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _txtPath = new TextBox
        {
            Location = new Point(24, top),
            Width = fieldW - 90,
            Height = fieldH,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Text = existingSite?.PhysicalPath ?? ""
        };

        var btnBrowse = new ModernButton
        {
            Text = "Browse...",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Location = new Point(_txtPath.Right + 8, top - 1),
            Width = 82,
            Height = fieldH,
            BorderRadius = 5,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnBrowse.Click += BtnBrowse_Click;

        top += fieldH + spacing;

        // 2. Domain Name
        var lblDomain = new Label
        {
            Text = "Domain Name (*.test):",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _txtDomain = new TextBox
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = fieldH,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Text = existingSite?.Domain ?? ""
        };
        top += fieldH + spacing;

        // 3. Document Root Subfolder
        var lblDocRoot = new Label
        {
            Text = "Document Root Subfolder:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _cmbDocRoot = new ComboBox
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = fieldH,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        _cmbDocRoot.Items.AddRange(["[Project Root]", "public", "htdocs"]);
        if (existingSite != null)
        {
            if (string.Equals(existingSite.DocumentRoot, existingSite.PhysicalPath, StringComparison.OrdinalIgnoreCase))
            {
                _cmbDocRoot.SelectedItem = "[Project Root]";
            }
            else
            {
                string rel = Path.GetRelativePath(existingSite.PhysicalPath, existingSite.DocumentRoot);
                _cmbDocRoot.Text = rel;
            }
        }
        else
        {
            _cmbDocRoot.SelectedIndex = 0;
        }
        top += fieldH + spacing;

        // 4. PHP Version Dropdown
        var lblPhp = new Label
        {
            Text = "PHP Version:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _cmbPhp = new ComboBox
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = fieldH,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            FlatStyle = FlatStyle.Flat
        };
        _cmbPhp.Items.Add($"Default (Active: {_config.ActivePhp})");

        var installedPhps = PhpService.GetInstalledVersions(_appRoot);
        foreach (var p in installedPhps)
        {
            _cmbPhp.Items.Add(p);
        }

        if (existingSite != null && !string.IsNullOrEmpty(existingSite.PhpVersion) && !existingSite.PhpVersion.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            int idx = _cmbPhp.Items.IndexOf(existingSite.PhpVersion);
            if (idx >= 0) _cmbPhp.SelectedIndex = idx;
            else
            {
                _cmbPhp.Items.Add(existingSite.PhpVersion);
                _cmbPhp.SelectedItem = existingSite.PhpVersion;
            }
        }
        else
        {
            _cmbPhp.SelectedIndex = 0;
        }
        top += fieldH + spacing;

        // 5. SSL Checkbox
        _chkSsl = new CheckBox
        {
            Text = " Enable SSL / HTTPS (Auto-generates self-signed TLS cert on port 443)",
            Location = new Point(24, top + 4),
            AutoSize = true,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8.75f, FontStyle.Regular),
            Checked = existingSite?.SslEnabled ?? false
        };
        top += fieldH + spacing;

        // Error message label
        _lblError = new Label
        {
            Location = new Point(24, top),
            Width = fieldW,
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8f),
            AutoSize = true,
            Text = ""
        };

        body.Controls.AddRange([
            lblPath, _txtPath, btnBrowse,
            lblDomain, _txtDomain,
            lblDocRoot, _cmbDocRoot,
            lblPhp, _cmbPhp,
            _chkSsl,
            _lblError
        ]);

        // Footer Action Buttons
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = ModernColors.Surface,
            Padding = new Padding(20, 10, 20, 12)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var p = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(p, 0, 0, pnlFooter.Width, 0);
        };

        var btnCancel = new ModernButton
        {
            Text = "Cancel",
            Width = 85,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(pnlFooter.ClientSize.Width - 195, 11)
        };
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var btnSave = new ModernButton
        {
            Text = existingSite == null ? "Add Site" : "Save Changes",
            IconKind = IconKind.Check,
            IconSize = 10,
            Width = 105,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(pnlFooter.ClientSize.Width - 100, 11)
        };
        btnSave.Click += BtnSave_Click;

        pnlFooter.Resize += (s, e) =>
        {
            btnSave.Location = new Point(pnlFooter.ClientSize.Width - btnSave.Width - 20, 11);
            btnCancel.Location = new Point(btnSave.Left - btnCancel.Width - 10, 11);
        };

        pnlFooter.Controls.Add(btnCancel);
        pnlFooter.Controls.Add(btnSave);

        Controls.Add(body);
        Controls.Add(pnlHeader);
        Controls.Add(pnlFooter);
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Select project directory",
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrEmpty(_txtPath.Text) && Directory.Exists(_txtPath.Text))
        {
            fbd.InitialDirectory = _txtPath.Text;
        }

        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            _txtPath.Text = fbd.SelectedPath;

            string folderName = Path.GetFileName(fbd.SelectedPath);
            if (string.IsNullOrWhiteSpace(_txtDomain.Text) || _txtDomain.Text.EndsWith(".test"))
            {
                _txtDomain.Text = $"{folderName.ToLowerInvariant()}.test";
            }

            // Auto-detect public/ or htdocs/
            string pub = Path.Combine(fbd.SelectedPath, "public");
            string htd = Path.Combine(fbd.SelectedPath, "htdocs");
            if (Directory.Exists(pub))
            {
                _cmbDocRoot.SelectedItem = "public";
            }
            else if (Directory.Exists(htd))
            {
                _cmbDocRoot.SelectedItem = "htdocs";
            }
            else
            {
                _cmbDocRoot.SelectedIndex = 0;
            }
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        _lblError.Text = "";

        string path = _txtPath.Text.Trim();
        string domain = _txtDomain.Text.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            _lblError.Text = "Please select a valid existing project folder.";
            _txtPath.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(domain))
        {
            _lblError.Text = "Please enter a valid domain name (e.g. project.test).";
            _txtDomain.Focus();
            return;
        }

        // Clean domain
        domain = domain.Replace("http://", "").Replace("https://", "").Trim('/');
        if (!domain.Contains('.'))
        {
            domain = $"{domain}.test";
        }

        // Calculate document root
        string docRoot = path;
        string selDoc = _cmbDocRoot.Text.Trim();
        if (!string.IsNullOrEmpty(selDoc) && !selDoc.Equals("[Project Root]", StringComparison.OrdinalIgnoreCase))
        {
            string subDir = Path.Combine(path, selDoc);
            if (Directory.Exists(subDir))
            {
                docRoot = subDir;
            }
            else
            {
                // Fallback to path if specified subfolder does not exist
                docRoot = path;
            }
        }

        // Calculate PHP version
        string phpVersion = "default";
        if (_cmbPhp.SelectedIndex > 0 && _cmbPhp.SelectedItem is string selected)
        {
            phpVersion = selected;
        }

        // Determine if external (outside /www)
        string wwwRoot = Path.GetFullPath(Path.Combine(_appRoot, "www"));
        string normPath = Path.GetFullPath(path);
        bool isExternal = !normPath.StartsWith(wwwRoot, StringComparison.OrdinalIgnoreCase);

        var site = _existingSite ?? new SiteItem();
        site.Name = Path.GetFileName(normPath);
        site.Domain = domain;
        site.PhysicalPath = normPath;
        site.DocumentRoot = docRoot;
        site.PhpVersion = phpVersion;
        site.SslEnabled = _chkSsl.Checked;
        site.IsExternal = isExternal;

        ResultSite = site;
        VirtualHostManager.AddOrUpdateSite(_appRoot, site);

        DialogResult = DialogResult.OK;
        Close();
    }
}
