using System.Drawing.Drawing2D;

namespace DevLiteServer.UI;

public enum IconKind
{
    None,
    Play,
    Stop,
    Folder,
    Terminal,
    Mail,
    Database,
    Refresh,
    Server,
    Globe,
    Lightning,
    Power,
    Check,
    Gear,
    Warning,
    Download,
    Trash
}

public static class VectorIcons
{
    public static void Draw(Graphics g, IconKind kind, RectangleF rect, Color color)
    {
        if (kind == IconKind.None || rect.Width <= 0 || rect.Height <= 0) return;

        var prevSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float x = rect.X;
        float y = rect.Y;
        float w = rect.Width;
        float h = rect.Height;

        using var pen = new Pen(color, Math.Max(w * 0.1f, 1.25f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(color);

        switch (kind)
        {
            case IconKind.Play:
                // Solid triangle
                using (var path = new GraphicsPath())
                {
                    path.AddPolygon(new[]
                    {
                        new PointF(x + (w * 0.28f), y + (h * 0.2f)),
                        new PointF(x + (w * 0.82f), y + (h * 0.5f)),
                        new PointF(x + (w * 0.28f), y + (h * 0.8f))
                    });
                    g.FillPath(brush, path);
                }
                break;

            case IconKind.Stop:
                // Solid rounded square
                float sqMargin = w * 0.25f;
                float sqSize = w - (sqMargin * 2);
                float sqRadius = Math.Max(sqSize * 0.18f, 1.5f);
                using (var path = CreateRoundedRect(new RectangleF(x + sqMargin, y + sqMargin, sqSize, sqSize), sqRadius))
                {
                    g.FillPath(brush, path);
                }
                break;

            case IconKind.Folder:
                // Folder outline
                using (var path = new GraphicsPath())
                {
                    // Body
                    float bx = x + (w * 0.12f);
                    float by = y + (h * 0.35f);
                    float bw = w * 0.76f;
                    float bh = h * 0.48f;
                    path.AddRectangle(new RectangleF(bx, by, bw, bh));
                    // Tab
                    path.AddLine(bx, by, bx, y + (h * 0.25f));
                    path.AddLine(bx, y + (h * 0.25f), bx + (w * 0.32f), y + (h * 0.25f));
                    path.AddLine(bx + (w * 0.32f), y + (h * 0.25f), bx + (w * 0.42f), by);
                    g.DrawPath(pen, path);
                }
                break;

            case IconKind.Terminal:
                // Prompt >_
                g.DrawLine(pen, x + (w * 0.2f), y + (h * 0.28f), x + (w * 0.45f), y + (h * 0.5f));
                g.DrawLine(pen, x + (w * 0.45f), y + (h * 0.5f), x + (w * 0.2f), y + (h * 0.72f));
                g.DrawLine(pen, x + (w * 0.55f), y + (h * 0.72f), x + (w * 0.82f), y + (h * 0.72f));
                break;

            case IconKind.Mail:
                // Envelope
                float mx = x + (w * 0.15f);
                float my = y + (h * 0.25f);
                float mw = w * 0.7f;
                float mh = h * 0.5f;
                g.DrawRectangle(pen, mx, my, mw, mh);
                // Flap
                g.DrawLine(pen, mx, my, mx + (mw * 0.5f), my + (mh * 0.55f));
                g.DrawLine(pen, mx + (mw * 0.5f), my + (mh * 0.55f), mx + mw, my);
                break;

            case IconKind.Database:
                // Cylinder discs
                float dx = x + (w * 0.2f);
                float dw = w * 0.6f;
                float dh = h * 0.24f;
                // Top ellipse
                g.DrawEllipse(pen, dx, y + (h * 0.18f), dw, dh);
                // Bottom arc and sides
                g.DrawArc(pen, dx, y + (h * 0.38f), dw, dh, 0, 180);
                g.DrawArc(pen, dx, y + (h * 0.58f), dw, dh, 0, 180);
                g.DrawLine(pen, dx, y + (h * 0.3f), dx, y + (h * 0.7f));
                g.DrawLine(pen, dx + dw, y + (h * 0.3f), dx + dw, y + (h * 0.7f));
                break;

            case IconKind.Refresh:
                // Circular arrows
                float rw = w * 0.6f;
                float rx = x + (w * 0.2f);
                float ry = y + (h * 0.2f);
                g.DrawArc(pen, rx, ry, rw, rw, 45, 270);
                // Arrow head
                float ax = rx + (rw * 0.85f);
                float ay = ry + (rw * 0.2f);
                g.DrawLine(pen, ax, ay, ax + (w * 0.1f), ay - (h * 0.1f));
                g.DrawLine(pen, ax, ay, ax - (w * 0.05f), ay - (h * 0.14f));
                break;

            case IconKind.Server:
                // 3 server rack lines
                float sx = x + (w * 0.18f);
                float sw = w * 0.64f;
                float sh = h * 0.2f;
                for (int i = 0; i < 3; i++)
                {
                    float sy = y + (h * (0.16f + (i * 0.26f)));
                    using var path = CreateRoundedRect(new RectangleF(sx, sy, sw, sh), 2f);
                    g.DrawPath(pen, path);
                    // LED dot
                    g.FillEllipse(brush, sx + sw - (w * 0.14f), sy + (sh * 0.35f), w * 0.07f, w * 0.07f);
                }
                break;

            case IconKind.Globe:
                // Circle + cross
                float gx = x + (w * 0.16f);
                float gw = w * 0.68f;
                g.DrawEllipse(pen, gx, y + (h * 0.16f), gw, gw);
                g.DrawLine(pen, gx, y + (h * 0.5f), gx + gw, y + (h * 0.5f));
                g.DrawEllipse(pen, gx + (gw * 0.25f), y + (h * 0.16f), gw * 0.5f, gw);
                break;

            case IconKind.Lightning:
                // Sharp bolt
                using (var path = new GraphicsPath())
                {
                    path.AddPolygon(new[]
                    {
                        new PointF(x + (w * 0.58f), y + (h * 0.12f)),
                        new PointF(x + (w * 0.32f), y + (h * 0.52f)),
                        new PointF(x + (w * 0.52f), y + (h * 0.52f)),
                        new PointF(x + (w * 0.42f), y + (h * 0.88f)),
                        new PointF(x + (w * 0.72f), y + (h * 0.45f)),
                        new PointF(x + (w * 0.52f), y + (h * 0.45f))
                    });
                    g.FillPath(brush, path);
                }
                break;

            case IconKind.Power:
                // Power / Standby symbol (IEC 60417-5009)
                {
                    // Top vertical stroke
                    g.DrawLine(pen, x + (w * 0.5f), y + (h * 0.12f), x + (w * 0.5f), y + (h * 0.46f));

                    // Circle arc with opening at top (start -60 deg, sweep 300 deg)
                    float inset = pen.Width * 0.7f + 1f;
                    var arcRect = new RectangleF(x + inset, y + inset, w - (inset * 2f), h - (inset * 2f));
                    g.DrawArc(pen, arcRect, -60, 300);
                }
                break;

            case IconKind.Check:
                g.DrawLines(pen, new[]
                {
                    new PointF(x + (w * 0.22f), y + (h * 0.52f)),
                    new PointF(x + (w * 0.44f), y + (h * 0.74f)),
                    new PointF(x + (w * 0.80f), y + (h * 0.28f))
                });
                break;

            case IconKind.Gear:
                {
                    float cx = x + (w * 0.5f);
                    float cy = y + (h * 0.5f);
                    float outerR = Math.Min(w, h) * 0.44f;
                    float innerR = outerR * 0.68f;
                    float holeR = outerR * 0.32f;

                    for (int i = 0; i < 6; i++)
                    {
                        double angle = i * Math.PI / 3.0;
                        float cos = (float)Math.Cos(angle);
                        float sin = (float)Math.Sin(angle);
                        g.DrawLine(pen, cx + (cos * innerR), cy + (sin * innerR), cx + (cos * outerR), cy + (sin * outerR));
                    }
                    g.DrawEllipse(pen, cx - innerR, cy - innerR, innerR * 2f, innerR * 2f);
                    g.DrawEllipse(pen, cx - holeR, cy - holeR, holeR * 2f, holeR * 2f);
                }
                break;

            case IconKind.Warning:
                {
                    // Triangle with exclamation mark
                    using var path = new GraphicsPath();
                    path.AddPolygon(new[]
                    {
                        new PointF(x + (w * 0.5f), y + (h * 0.12f)),
                        new PointF(x + (w * 0.88f), y + (h * 0.86f)),
                        new PointF(x + (w * 0.12f), y + (h * 0.86f))
                    });
                    using var thinPen = new Pen(color, Math.Max(w * 0.08f, 1.2f))
                    {
                        LineJoin = LineJoin.Round
                    };
                    g.DrawPath(thinPen, path);

                    // Exclamation vertical stroke & dot
                    g.DrawLine(thinPen, x + (w * 0.5f), y + (h * 0.38f), x + (w * 0.5f), y + (h * 0.60f));
                    float dotSize = Math.Max(w * 0.08f, 1.5f);
                    g.FillEllipse(brush, x + (w * 0.5f) - (dotSize / 2f), y + (h * 0.72f), dotSize, dotSize);
                }
                break;

            case IconKind.Download:
                {
                    // Downward arrow
                    float cx = x + (w * 0.5f);
                    float arrowTop = y + (h * 0.16f);
                    float arrowBot = y + (h * 0.62f);
                    g.DrawLine(pen, cx, arrowTop, cx, arrowBot);
                    g.DrawLine(pen, cx - (w * 0.22f), arrowBot - (h * 0.20f), cx, arrowBot);
                    g.DrawLine(pen, cx + (w * 0.22f), arrowBot - (h * 0.20f), cx, arrowBot);

                    // Bottom tray
                    float trayLeft = x + (w * 0.16f);
                    float trayRight = x + (w * 0.84f);
                    float trayTop = y + (h * 0.68f);
                    float trayBot = y + (h * 0.86f);
                    g.DrawLine(pen, trayLeft, trayTop, trayLeft, trayBot);
                    g.DrawLine(pen, trayLeft, trayBot, trayRight, trayBot);
                    g.DrawLine(pen, trayRight, trayBot, trayRight, trayTop);
                }
                break;

            case IconKind.Trash:
                {
                    // Lid
                    float lx1 = x + (w * 0.16f);
                    float lx2 = x + (w * 0.84f);
                    float ly = y + (h * 0.26f);
                    g.DrawLine(pen, lx1, ly, lx2, ly);
                    // Handle
                    g.DrawLine(pen, x + (w * 0.38f), ly, x + (w * 0.38f), y + (h * 0.15f));
                    g.DrawLine(pen, x + (w * 0.38f), y + (h * 0.15f), x + (w * 0.62f), y + (h * 0.15f));
                    g.DrawLine(pen, x + (w * 0.62f), y + (h * 0.15f), x + (w * 0.62f), ly);

                    // Body
                    float bx1 = x + (w * 0.24f);
                    float bx2 = x + (w * 0.76f);
                    float byTop = ly;
                    float byBot = y + (h * 0.86f);
                    g.DrawLine(pen, bx1, byTop, bx1 + (w * 0.04f), byBot);
                    g.DrawLine(pen, bx1 + (w * 0.04f), byBot, bx2 - (w * 0.04f), byBot);
                    g.DrawLine(pen, bx2 - (w * 0.04f), byBot, bx2, byTop);

                    // Inner lines
                    g.DrawLine(pen, x + (w * 0.42f), ly + (h * 0.15f), x + (w * 0.42f), byBot - (h * 0.10f));
                    g.DrawLine(pen, x + (w * 0.58f), ly + (h * 0.15f), x + (w * 0.58f), byBot - (h * 0.10f));
                }
                break;
        }

        g.SmoothingMode = prevSmoothing;
    }

    private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
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
