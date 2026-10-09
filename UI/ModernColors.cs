namespace DevLiteServer.UI;

public static class ModernColors
{
    // Canvas & Containers (Dark Slate OLED)
    public static readonly Color Background = Color.FromArgb(11, 15, 25);       // #0B0F19 (Deep canvas)
    public static readonly Color Surface = Color.FromArgb(17, 24, 39);          // #111827 (Header / Footer / Panels)
    public static readonly Color SurfaceHover = Color.FromArgb(31, 41, 55);     // #1F2937
    public static readonly Color Card = Color.FromArgb(24, 33, 50);             // #182132 (Cards)
    public static readonly Color CardHover = Color.FromArgb(30, 41, 62);        // #1E293E (Card hover)

    // Borders
    public static readonly Color BorderSubtle = Color.FromArgb(31, 41, 55);     // #1F2937
    public static readonly Color Border = Color.FromArgb(51, 65, 85);           // #334155
    public static readonly Color BorderLight = Color.FromArgb(71, 85, 105);     // #475569

    // Brand Accents
    public static readonly Color Primary = Color.FromArgb(56, 189, 248);        // #38BDF8 (Sky/Cyan Accent)
    public static readonly Color PrimaryHover = Color.FromArgb(14, 165, 233);   // #0EA5E9
    public static readonly Color PrimaryBg = Color.FromArgb(12, 74, 110);       // #0C4A6E

    // Semantic States
    public static readonly Color Success = Color.FromArgb(34, 197, 94);         // #22C55E (Emerald Green)
    public static readonly Color SuccessHover = Color.FromArgb(22, 163, 74);    // #16A34A
    public static readonly Color SuccessBg = Color.FromArgb(20, 83, 45);        // #14532D (Subtle dark emerald)

    public static readonly Color Danger = Color.FromArgb(244, 63, 94);          // #F43F5E (Rose Red)
    public static readonly Color DangerHover = Color.FromArgb(225, 29, 72);     // #E11D48
    public static readonly Color DangerBg = Color.FromArgb(136, 19, 55);        // #881337

    public static readonly Color Warning = Color.FromArgb(245, 158, 11);        // #F59E0B (Amber)
    public static readonly Color WarningHover = Color.FromArgb(217, 119, 6);    // #D97706
    public static readonly Color WarningBg = Color.FromArgb(120, 53, 15);       // #78350F

    // Typography
    public static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);   // #F8FAFC (Slate 50)
    public static readonly Color TextSecondary = Color.FromArgb(148, 163, 184); // #94A3B8 (Slate 400)
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);     // #64748B (Slate 500)
}
