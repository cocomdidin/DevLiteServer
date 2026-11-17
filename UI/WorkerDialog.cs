using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class WorkerDialog : Form
{
    private readonly string _appRoot;
    private readonly WorkerItem? _existingWorker;

    private readonly ModernTextBox _txtName;
    private readonly ModernTextBox _txtCommand;
    private readonly ModernTextBox _txtWorkDir;
    private readonly ModernComboBox _cmbSite;
    private readonly CheckBox _chkAutoStart;
    private readonly CheckBox _chkRestartOnCrash;
    private readonly Label _lblError;

    public WorkerItem? ResultWorker { get; private set; }

    public WorkerDialog(string appRoot, WorkerItem? existingWorker = null, SiteItem? prefillSite = null)
    {
        _appRoot = appRoot;
        _existingWorker = existingWorker;

        Text = existingWorker == null ? "Add Background Worker - Dev Lite Server" : "Edit Background Worker";
        ClientSize = new Size(540, 490);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 9f);

        // Header
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
            Text = existingWorker == null ? "Add Background Worker" : $"Edit Worker: {existingWorker.Name}",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 12)
        };

        var lblSub = new Label
        {
            Text = "Run queues, dev servers (npm run dev), or schedulers in background via Windows JobObject.",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(21, 36)
        };
        pnlHeader.Controls.AddRange([lblTitle, lblSub]);

        // Body
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

        // 1. Worker Name
        var lblName = new Label
        {
            Text = "Worker Name:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _txtName = new ModernTextBox
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = fieldH,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderRadius = 6,
            Text = existingWorker?.Name ?? (prefillSite != null ? $"Dev Server - {prefillSite.Name}" : "")
        };
        top += fieldH + spacing;

        // 2. Command
        var lblCmd = new Label
        {
            Text = "Command (executed via cmd.exe /c with PHP, Node, Composer in PATH):",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _txtCommand = new ModernTextBox
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = fieldH,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderRadius = 6,
            Text = existingWorker?.Command ?? (prefillSite != null && prefillSite.IsProxy ? "npm run dev" : "")
        };
        top += fieldH + 6;

        // Quick Preset Chips
        var pnlPresets = new FlowLayoutPanel
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = 28,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        string[] presets = ["npm run dev", "php artisan queue:listen", "php artisan schedule:work", "php artisan octane:start"];
        foreach (var preset in presets)
        {
            var btn = new ModernButton
            {
                Text = preset,
                Height = 22,
                AutoSize = false,
                Width = TextRenderer.MeasureText(preset, new Font("Segoe UI", 7.5f)).Width + 14,
                BorderRadius = 4,
                ShowBorder = true,
                NormalColor = ModernColors.Surface,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.TextSecondary,
                Font = new Font("Segoe UI", 7.5f),
                Margin = new Padding(0, 0, 6, 0)
            };
            btn.Click += (s, e) =>
            {
                _txtCommand.Text = preset;
                if (string.IsNullOrWhiteSpace(_txtName.Text))
                {
                    _txtName.Text = preset;
                }
            };
            pnlPresets.Controls.Add(btn);
        }
        top += 34;

        // 3. Working Directory
        var lblDir = new Label
        {
            Text = "Working Directory:",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _txtWorkDir = new ModernTextBox
        {
            Location = new Point(24, top),
            Width = fieldW - 90,
            Height = fieldH,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary,
            BorderRadius = 6,
            Text = existingWorker?.WorkingDirectory ?? prefillSite?.PhysicalPath ?? Path.Combine(appRoot, "www")
        };

        var btnBrowse = new ModernButton
        {
            Text = "Browse...",
            IconKind = IconKind.Folder,
            IconSize = 10,
            Location = new Point(_txtWorkDir.Right + 8, top - 1),
            Width = 82,
            Height = fieldH,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Select Worker Working Directory",
                InitialDirectory = Directory.Exists(_txtWorkDir.Text) ? _txtWorkDir.Text : Path.Combine(_appRoot, "www")
            };
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                _txtWorkDir.Text = fbd.SelectedPath;
            }
        };
        top += fieldH + spacing;

        // 4. Linked Site (Optional)
        var lblSite = new Label
        {
            Text = "Linked Site (Optional):",
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(24, top),
            AutoSize = true
        };
        top += labelH + 2;

        _cmbSite = new ModernComboBox
        {
            Location = new Point(24, top),
            Width = fieldW,
            Height = fieldH,
            BackColor = ModernColors.Card,
            ForeColor = ModernColors.TextPrimary
        };
        _cmbSite.Items.Add("(None)");
        var sites = VirtualHostManager.GetAllSites(_appRoot);
        foreach (var site in sites)
        {
            _cmbSite.Items.Add(site.Domain);
        }

        string? targetDomain = existingWorker?.SiteDomain ?? prefillSite?.Domain;
        if (!string.IsNullOrEmpty(targetDomain) && _cmbSite.Items.Contains(targetDomain))
        {
            _cmbSite.SelectedItem = targetDomain;
        }
        else
        {
            _cmbSite.SelectedIndex = 0;
        }
        top += fieldH + spacing;

        // 5. Options
        _chkAutoStart = new CheckBox
        {
            Text = "Auto-start this worker when DevLiteServer launches",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8.75f),
            Location = new Point(24, top),
            AutoSize = true,
            Checked = existingWorker?.AutoStart ?? false
        };
        top += 26;

        _chkRestartOnCrash = new CheckBox
        {
            Text = "Auto-restart if process exits with non-zero code (crash)",
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 8.75f),
            Location = new Point(24, top),
            AutoSize = true,
            Checked = existingWorker?.RestartOnCrash ?? false
        };
        top += 28;

        _lblError = new Label
        {
            Text = "",
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8.25f),
            Location = new Point(24, top),
            Width = fieldW,
            Height = 20
        };

        body.Controls.AddRange([
            lblName, _txtName,
            lblCmd, _txtCommand, pnlPresets,
            lblDir, _txtWorkDir, btnBrowse,
            lblSite, _cmbSite,
            _chkAutoStart, _chkRestartOnCrash,
            _lblError
        ]);

        // Footer
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = ModernColors.Surface,
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var p = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(p, 0, 0, pnlFooter.Width, 0);
        };

        var btnCancel = new ModernButton
        {
            Text = "Cancel",
            Width = 88,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Location = new Point(ClientSize.Width - 200, 12),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var btnSave = new ModernButton
        {
            Text = existingWorker == null ? "Create Worker" : "Save Changes",
            IconKind = IconKind.Check,
            IconSize = 10,
            Width = 112,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Location = new Point(ClientSize.Width - 104, 12),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        btnSave.Click += BtnSave_Click;

        pnlFooter.Controls.AddRange([btnCancel, btnSave]);

        Controls.Add(body);
        Controls.Add(pnlFooter);
        Controls.Add(pnlHeader);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        string name = _txtName.Text.Trim();
        string cmd = _txtCommand.Text.Trim();
        string dir = _txtWorkDir.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            _lblError.Text = "Worker name is required.";
            return;
        }
        if (string.IsNullOrEmpty(cmd))
        {
            _lblError.Text = "Command is required.";
            return;
        }
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            _lblError.Text = "Working directory does not exist.";
            return;
        }

        string? selectedSite = _cmbSite.SelectedIndex > 0 ? _cmbSite.SelectedItem?.ToString() : null;

        if (_existingWorker != null)
        {
            _existingWorker.Name = name;
            _existingWorker.Command = cmd;
            _existingWorker.WorkingDirectory = dir;
            _existingWorker.SiteDomain = selectedSite;
            _existingWorker.AutoStart = _chkAutoStart.Checked;
            _existingWorker.RestartOnCrash = _chkRestartOnCrash.Checked;
            ResultWorker = _existingWorker;
        }
        else
        {
            ResultWorker = new WorkerItem
            {
                Name = name,
                Command = cmd,
                WorkingDirectory = dir,
                SiteDomain = selectedSite,
                AutoStart = _chkAutoStart.Checked,
                RestartOnCrash = _chkRestartOnCrash.Checked
            };
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
