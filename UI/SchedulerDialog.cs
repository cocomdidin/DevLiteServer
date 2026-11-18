using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class SchedulerDialog : Form
{
    private readonly string _appRoot;
    private readonly SchedulerItem? _existingTask;

    private readonly ModernTextBox _txtName;
    private readonly ModernTextBox _txtCommand;
    private readonly ModernTextBox _txtWorkDir;
    private readonly ModernTextBox _txtInterval;
    private readonly ModernComboBox _cmbSite;
    private readonly CheckBox _chkAutoStart;
    private readonly Label _lblError;

    public SchedulerItem? ResultTask { get; private set; }

    public SchedulerDialog(string appRoot, SchedulerItem? existingTask = null)
    {
        _appRoot = appRoot;
        _existingTask = existingTask;

        Text = existingTask == null ? "Add Scheduled Task - Dev Lite Server" : "Edit Scheduled Task";
        ClientSize = new Size(540, 520);
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
            Height = 64,
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
            Text = existingTask == null ? "Add Scheduled Task" : $"Edit Schedule: {existingTask.Name}",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 12)
        };

        var lblSub = new Label
        {
            Text = "Run periodic one-shot commands (e.g. php artisan schedule:run every 60s).",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(21, 37)
        };

        pnlHeader.Controls.AddRange([lblTitle, lblSub]);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 16, 22, 10)
        };

        int y = 14;
        Label MakeLabel(string text, int top) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(22, top)
        };

        // 1. Site selector
        body.Controls.Add(MakeLabel("Associated Site (Optional)", y));
        y += 20;

        _cmbSite = new ModernComboBox
        {
            Location = new Point(22, y),
            Width = 496,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        var sites = VirtualHostManager.GetAllSites(_appRoot);
        _cmbSite.Items.Add("-- Custom Directory --");
        foreach (var s in sites)
        {
            _cmbSite.Items.Add(s.Domain);
        }
        _cmbSite.SelectedIndex = 0;
        body.Controls.Add(_cmbSite);
        y += 42;

        // 2. Task Name
        body.Controls.Add(MakeLabel("Schedule Name", y));
        y += 20;
        _txtName = new ModernTextBox
        {
            Location = new Point(22, y),
            Width = 496,
            Height = 32,
            Text = existingTask?.Name ?? "Laravel Scheduler"
        };
        body.Controls.Add(_txtName);
        y += 44;

        // 3. Command
        body.Controls.Add(MakeLabel("Command", y));
        y += 20;
        _txtCommand = new ModernTextBox
        {
            Location = new Point(22, y),
            Width = 496,
            Height = 32,
            Text = existingTask?.Command ?? "php artisan schedule:run"
        };
        body.Controls.Add(_txtCommand);
        y += 44;

        // 4. Interval (seconds) + quick presets
        body.Controls.Add(MakeLabel("Interval (seconds)", y));
        y += 20;
        _txtInterval = new ModernTextBox
        {
            Location = new Point(22, y),
            Width = 100,
            Height = 32,
            Text = (existingTask?.IntervalSeconds ?? 60).ToString()
        };
        body.Controls.Add(_txtInterval);

        int px = 132;
        foreach (var (label, sec) in new[] { ("10s", 10), ("30s", 30), ("60s", 60), ("5m", 300) })
        {
            var btnPreset = new ModernButton
            {
                Text = label,
                Width = 54,
                Height = 32,
                BorderRadius = 6,
                ShowBorder = true,
                NormalColor = ModernColors.Card,
                HoverColor = ModernColors.SurfaceHover,
                ForeColor = ModernColors.TextPrimary,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Location = new Point(px, y)
            };
            btnPreset.Click += (s, e) => _txtInterval.Text = sec.ToString();
            body.Controls.Add(btnPreset);
            px += 60;
        }
        y += 44;

        // 5. Working Directory
        body.Controls.Add(MakeLabel("Working Directory", y));
        y += 20;
        _txtWorkDir = new ModernTextBox
        {
            Location = new Point(22, y),
            Width = 408,
            Height = 32,
            Text = existingTask?.WorkingDirectory ?? Path.Combine(_appRoot, "www")
        };
        var btnBrowse = new ModernButton
        {
            Text = "Browse",
            Width = 80,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Location = new Point(438, y),
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold)
        };
        btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog { SelectedPath = _txtWorkDir.Text };
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                _txtWorkDir.Text = fbd.SelectedPath;
            }
        };
        body.Controls.AddRange([_txtWorkDir, btnBrowse]);
        y += 42;

        // 6. Auto-start
        _chkAutoStart = new CheckBox
        {
            Text = "Auto-start schedule when Dev Lite Server opens",
            Checked = existingTask?.AutoStart ?? false,
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(22, y)
        };
        body.Controls.Add(_chkAutoStart);
        y += 28;

        _lblError = new Label
        {
            Text = "",
            ForeColor = ModernColors.Danger,
            Font = new Font("Segoe UI", 8.25f),
            AutoSize = true,
            Location = new Point(22, y)
        };
        body.Controls.Add(_lblError);

        _cmbSite.SelectedIndexChanged += (s, e) =>
        {
            if (_cmbSite.SelectedIndex > 0)
            {
                var site = sites[_cmbSite.SelectedIndex - 1];
                _txtWorkDir.Text = site.PhysicalPath;
                if (existingTask == null)
                {
                    _txtName.Text = $"{site.Name} - Schedule";
                }
            }
        };

        if (existingTask != null && !string.IsNullOrEmpty(existingTask.SiteId))
        {
            int idx = sites.FindIndex(s => s.Id == existingTask.SiteId);
            if (idx >= 0) _cmbSite.SelectedIndex = idx + 1;
        }

        // Footer
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Width = ClientSize.Width,
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
            Location = new Point(ClientSize.Width - 240, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var btnSave = new ModernButton
        {
            Text = existingTask == null ? "Create Schedule" : "Save Changes",
            IconKind = IconKind.Check,
            IconSize = 10,
            Width = 124,
            Height = 32,
            BorderRadius = 6,
            ShowBorder = false,
            NormalColor = ModernColors.Primary,
            HoverColor = ModernColors.PrimaryHover,
            Location = new Point(ClientSize.Width - 144, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        btnSave.Click += (s, e) =>
        {
            string name = _txtName.Text.Trim();
            string cmd = _txtCommand.Text.Trim();
            string dir = _txtWorkDir.Text.Trim();

            if (string.IsNullOrEmpty(name)) { _lblError.Text = "Schedule name is required."; return; }
            if (string.IsNullOrEmpty(cmd)) { _lblError.Text = "Command is required."; return; }
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { _lblError.Text = "Working directory does not exist."; return; }
            if (!int.TryParse(_txtInterval.Text.Trim(), out int interval) || interval < 5)
            {
                _lblError.Text = "Interval must be at least 5 seconds.";
                return;
            }

            string siteId = "";
            if (_cmbSite.SelectedIndex > 0)
            {
                siteId = sites[_cmbSite.SelectedIndex - 1].Id;
            }

            ResultTask = new SchedulerItem
            {
                Id = _existingTask?.Id ?? Guid.NewGuid().ToString("N")[..8],
                Name = name,
                Command = cmd,
                WorkingDirectory = dir,
                SiteId = siteId,
                IntervalSeconds = interval,
                AutoStart = _chkAutoStart.Checked
            };

            DialogResult = DialogResult.OK;
            Close();
        };

        pnlFooter.Controls.AddRange([btnCancel, btnSave]);

        Controls.Add(body);
        Controls.Add(pnlFooter);
        Controls.Add(pnlHeader);
    }
}
