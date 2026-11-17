using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class WorkerLogsDialog : Form
{
    private readonly WorkerItem _worker;
    private readonly TextBox _txtLogs;
    private readonly ModernButton _btnClear;
    private readonly StatusPill _pillStatus;
    private Action<string>? _logHandler;
    private Action<string>? _statusHandler;

    public WorkerLogsDialog(WorkerItem worker)
    {
        _worker = worker;

        Text = $"Worker Logs - {worker.Name}";
        ClientSize = new Size(680, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ModernColors.Background;
        ForeColor = ModernColors.TextPrimary;
        Font = new Font("Segoe UI", 9f);
        MinimizeBox = true;
        MaximizeBox = true;

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = ModernColors.Surface,
            Padding = new Padding(20, 12, 20, 12)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var p = new Pen(ModernColors.BorderSubtle, 1);
            e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = worker.Name,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            AutoSize = true,
            Location = new Point(18, 10)
        };

        var lblCmd = new Label
        {
            Text = $"Command: {worker.Command}  |  Directory: {worker.WorkingDirectory}",
            Font = new Font("Segoe UI", 8.25f),
            ForeColor = ModernColors.TextSecondary,
            AutoSize = true,
            Location = new Point(19, 34)
        };

        _pillStatus = new StatusPill
        {
            Text = worker.Status,
            Status = worker.IsRunning ? Services.ServiceStatus.Running : Services.ServiceStatus.Stopped,
            Location = new Point(ClientSize.Width - 190, 16),
            Size = new Size(80, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        _btnClear = new ModernButton
        {
            Text = "Clear",
            IconKind = IconKind.Trash,
            IconSize = 10,
            Location = new Point(ClientSize.Width - 95, 14),
            Width = 75,
            Height = 28,
            BorderRadius = 6,
            ShowBorder = true,
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        pnlHeader.Controls.AddRange([lblTitle, lblCmd, _pillStatus, _btnClear]);

        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16)
        };

        _txtLogs = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(16, 20, 28),
            ForeColor = Color.FromArgb(190, 210, 230),
            Font = new Font("Cascadia Code", 8.5f),
            BorderStyle = BorderStyle.None
        };
        _btnClear.Click += (s, e) => _txtLogs.Clear();

        pnlBody.Controls.Add(_txtLogs);

        Controls.Add(pnlBody);
        Controls.Add(pnlHeader);

        // Populate initial logs
        lock (worker.LogBuffer)
        {
            if (worker.LogBuffer.Count > 0)
            {
                _txtLogs.Text = string.Join(Environment.NewLine, worker.LogBuffer) + Environment.NewLine;
                _txtLogs.SelectionStart = _txtLogs.Text.Length;
                _txtLogs.ScrollToCaret();
            }
        }

        _logHandler = line =>
        {
            if (IsDisposed) return;
            try
            {
                Invoke(() =>
                {
                    _txtLogs.AppendText(line + Environment.NewLine);
                });
            }
            catch { }
        };

        _statusHandler = status =>
        {
            if (IsDisposed) return;
            try
            {
                Invoke(() =>
                {
                    _pillStatus.Text = status;
                    _pillStatus.Status = status == "Running" ? Services.ServiceStatus.Running : Services.ServiceStatus.Stopped;
                });
            }
            catch { }
        };

        worker.LogReceived += _logHandler;
        worker.StatusChanged += _statusHandler;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_logHandler != null) _worker.LogReceived -= _logHandler;
        if (_statusHandler != null) _worker.StatusChanged -= _statusHandler;
        base.OnFormClosed(e);
    }
}
