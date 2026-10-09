using System.Drawing.Drawing2D;
using DevLiteServer.Core;

namespace DevLiteServer.UI;

public class PortConflictDialog : Form
{
    private readonly string _serviceName;
    private readonly int _conflictedPort;
    private int _selectedPort;

    private readonly NumericUpDown _numPort;
    private readonly ModernButton _btnReassign;
    private readonly ModernButton _btnCancel;

    public int SelectedPort => _selectedPort;

    public PortConflictDialog(string serviceName, int conflictedPort, int suggestedPort)
    {
        _serviceName = serviceName;
        _conflictedPort = conflictedPort;
        _selectedPort = suggestedPort;

        Text = "Port Conflict Detected";
        ClientSize = new Size(480, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ModernColors.Surface;
        ForeColor = ModernColors.TextPrimary;

        // 1. Header with Warning Icon
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(20, 14, 20, 10),
            BackColor = Color.FromArgb(20, 28, 44)
        };
        headerPanel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Warning Icon badge
            var badgeRect = new RectangleF(20, 16, 36, 36);
            using var badgeBg = new SolidBrush(Color.FromArgb(45, 30, 10));
            using var badgeBorder = new Pen(Color.FromArgb(245, 158, 11, 160), 1f);
            g.FillEllipse(badgeBg, badgeRect);
            g.DrawEllipse(badgeBorder, badgeRect);

            var iconRect = new RectangleF(28, 24, 20, 20);
            VectorIcons.Draw(g, IconKind.Warning, iconRect, ModernColors.Warning);

            using var borderPen = new Pen(ModernColors.BorderSubtle, 1);
            g.DrawLine(borderPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = "Port Conflict Detected",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(68, 14),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = $"Service '{serviceName}' cannot bind to port {conflictedPort}.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(69, 38),
            AutoSize = true
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSub);

        // 2. Body Card
        var bodyCard = new Panel
        {
            Location = new Point(20, 86),
            Size = new Size(440, 130),
            BackColor = ModernColors.Card,
            Padding = new Padding(16, 12, 16, 12)
        };
        bodyCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = 1f;
            float half = stroke / 2f;
            var rect = new RectangleF(half, half, bodyCard.Width - stroke, bodyCard.Height - stroke);
            using var path = CreateRoundedRectangle(rect, 6f);
            using var pen = new Pen(ModernColors.BorderSubtle, stroke);
            g.DrawPath(pen, path);
        };

        var lblExplanation = new Label
        {
            Text = $"Port {conflictedPort} is occupied by another application or service.\nChoose an alternate available port to proceed:",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ModernColors.TextSecondary,
            Location = new Point(16, 12),
            Size = new Size(408, 36)
        };

        var lblInputTag = new Label
        {
            Text = "New Port:",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = ModernColors.TextPrimary,
            Location = new Point(16, 56),
            AutoSize = true
        };

        _numPort = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 65535,
            Value = suggestedPort,
            Location = new Point(88, 54),
            Width = 110,
            BackColor = ModernColors.Surface,
            ForeColor = ModernColors.TextPrimary,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblAvailableStatus = new Label
        {
            Text = $"✓ Port {suggestedPort} is free",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = ModernColors.Success,
            Location = new Point(210, 56),
            AutoSize = true
        };

        _btnReassign = new ModernButton
        {
            Text = $"Reassign & Start",
            IconKind = IconKind.Play,
            IconSize = 10,
            Width = 140,
            Height = 34,
            Location = new Point(320, 234),
            NormalColor = ModernColors.Success,
            HoverColor = ModernColors.SuccessHover,
            PressedColor = ModernColors.SuccessBg,
            BorderRadius = 6,
            ShowBorder = false,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        _btnReassign.Click += (s, e) =>
        {
            _selectedPort = (int)_numPort.Value;
            DialogResult = DialogResult.OK;
            Close();
        };

        _btnCancel = new ModernButton
        {
            Text = "Cancel",
            Width = 84,
            Height = 34,
            Location = new Point(226, 234),
            NormalColor = ModernColors.Card,
            HoverColor = ModernColors.SurfaceHover,
            PressedColor = ModernColors.Background,
            BorderRadius = 6,
            ShowBorder = true,
            BorderLineColor = ModernColors.BorderSubtle,
            ForeColor = ModernColors.TextSecondary,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        _btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _numPort.ValueChanged += (s, e) =>
        {
            int p = (int)_numPort.Value;
            if (PortChecker.IsPortOccupied(p))
            {
                lblAvailableStatus.Text = $"✗ Port {p} is also occupied";
                lblAvailableStatus.ForeColor = ModernColors.Danger;
                _btnReassign.Enabled = false;
            }
            else
            {
                lblAvailableStatus.Text = $"✓ Port {p} is free";
                lblAvailableStatus.ForeColor = ModernColors.Success;
                _btnReassign.Enabled = true;
            }
        };

        var lblHint = new Label
        {
            Text = "Configuration (config.ini) and templates will be updated automatically.",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
            ForeColor = ModernColors.TextMuted,
            Location = new Point(16, 92),
            AutoSize = true
        };

        bodyCard.Controls.Add(lblExplanation);
        bodyCard.Controls.Add(lblInputTag);
        bodyCard.Controls.Add(_numPort);
        bodyCard.Controls.Add(lblAvailableStatus);
        bodyCard.Controls.Add(lblHint);

        Controls.Add(headerPanel);
        Controls.Add(bodyCard);
        Controls.Add(_btnReassign);
        Controls.Add(_btnCancel);

        AcceptButton = _btnReassign;
        CancelButton = _btnCancel;
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
