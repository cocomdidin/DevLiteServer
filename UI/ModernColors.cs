namespace DevLiteServer.UI;

public static class ModernColors
{
    // Canvas & Containers (Deep Midnight Slate / Deep Cyan-Navy)
    public static readonly Color Background = Color.FromArgb(13, 23, 34);         // #0D1722 (Deep Midnight Blue canvas)
    public static readonly Color Surface = Color.FromArgb(17, 29, 43);            // #111D2B (Header / Footer / Sidebar)
    public static readonly Color SurfaceHover = Color.FromArgb(30, 46, 62);       // #1E2E3E (Sidebar & control hover)
    public static readonly Color Card = Color.FromArgb(24, 37, 53);               // #182535 (Slate Cyan Cards)
    public static readonly Color CardHover = Color.FromArgb(35, 52, 72);          // #233448 (Card hover)

    // Borders
    public static readonly Color BorderSubtle = Color.FromArgb(25, 37, 54);       // #192536 (Subtle separators)
    public static readonly Color Border = Color.FromArgb(50, 68, 88);             // #324458 (Card & container borders)
    public static readonly Color BorderLight = Color.FromArgb(64, 83, 100);       // #405364 (Highlighted borders)

    // Brand Accents (Vibrant Cobalt Blue & Active Nav Indicator)
    public static readonly Color Primary = Color.FromArgb(64, 120, 240);          // #4078F0 (Vibrant Cobalt Blue)
    public static readonly Color PrimaryHover = Color.FromArgb(45, 100, 215);     // #2D64D7
    public static readonly Color PrimaryBg = Color.FromArgb(38, 64, 120, 240);    // Translucent Blue tint (15%)

    // Semantic States (Vibrant Mint / Sea Green & Rose Coral)
    public static readonly Color Success = Color.FromArgb(32, 196, 156);          // #20C49C (Vibrant Mint / Sea Green)
    public static readonly Color SuccessHover = Color.FromArgb(24, 170, 134);     // #18AA86
    public static readonly Color SuccessBg = Color.FromArgb(30, 80, 72);          // #1E5048 (Muted Emerald / Teal badge fill)

    public static readonly Color Danger = Color.FromArgb(220, 68, 85);            // #DC4455 (Rose Coral)
    public static readonly Color DangerHover = Color.FromArgb(195, 50, 65);       // #C33241
    public static readonly Color DangerBg = Color.FromArgb(80, 32, 44);           // #50202C (Translucent deep rose tint)

    public static readonly Color Warning = Color.FromArgb(245, 166, 35);          // #F5A623 (Amber Warm)
    public static readonly Color WarningHover = Color.FromArgb(220, 145, 20);     // #DC9114
    public static readonly Color WarningBg = Color.FromArgb(40, 245, 166, 35);    // Translucent Amber tint

    // Typography
    public static readonly Color TextPrimary = Color.FromArgb(240, 245, 250);     // #F0F5FA (Clean high-contrast crisp text)
    public static readonly Color TextSecondary = Color.FromArgb(154, 172, 192);   // #9AACAC (Cool Slate text)
    public static readonly Color TextMuted = Color.FromArgb(102, 122, 145);       // #667A91 (Muted slate blue captions)
}
