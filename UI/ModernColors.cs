namespace DevLiteServer.UI;

public static class ModernColors
{
    // Canvas & Containers (Obsidian / Zinc-950 Neutral Dark)
    public static readonly Color Background = Color.FromArgb(9, 9, 11);         // #09090B (Zinc 950 deep canvas)
    public static readonly Color Surface = Color.FromArgb(18, 18, 21);          // #121215 (Header / Footer / Sidebar)
    public static readonly Color SurfaceHover = Color.FromArgb(28, 28, 33);     // #1C1C21
    public static readonly Color Card = Color.FromArgb(24, 24, 27);             // #18181B (Zinc 900 Cards)
    public static readonly Color CardHover = Color.FromArgb(34, 34, 38);        // #222226 (Card hover)

    // Borders
    public static readonly Color BorderSubtle = Color.FromArgb(39, 39, 42);     // #27272A (Zinc 800)
    public static readonly Color Border = Color.FromArgb(63, 63, 70);           // #3F3F46 (Zinc 700)
    public static readonly Color BorderLight = Color.FromArgb(82, 82, 91);      // #52525B (Zinc 600)

    // Brand Accents (Electric Indigo)
    public static readonly Color Primary = Color.FromArgb(99, 102, 241);        // #6366F1 (Indigo 500)
    public static readonly Color PrimaryHover = Color.FromArgb(79, 70, 229);    // #4F46E5 (Indigo 600)
    public static readonly Color PrimaryBg = Color.FromArgb(35, 99, 102, 241);  // Translucent Indigo tint (14%)

    // Semantic States
    public static readonly Color Success = Color.FromArgb(16, 185, 129);        // #10B981 (Emerald 500)
    public static readonly Color SuccessHover = Color.FromArgb(5, 150, 105);    // #059669
    public static readonly Color SuccessBg = Color.FromArgb(30, 16, 185, 129);  // Translucent Emerald tint (12%)

    public static readonly Color Danger = Color.FromArgb(244, 63, 94);          // #F43F5E (Rose 500)
    public static readonly Color DangerHover = Color.FromArgb(225, 29, 72);     // #E11D48
    public static readonly Color DangerBg = Color.FromArgb(32, 244, 63, 94);    // Translucent Rose tint (12%)

    public static readonly Color Warning = Color.FromArgb(245, 158, 11);        // #F59E0B (Amber 500)
    public static readonly Color WarningHover = Color.FromArgb(217, 119, 6);    // #D97706
    public static readonly Color WarningBg = Color.FromArgb(32, 245, 158, 11);  // Translucent Amber tint (12%)

    // Typography
    public static readonly Color TextPrimary = Color.FromArgb(250, 250, 250);   // #FAFAFA (Zinc 50)
    public static readonly Color TextSecondary = Color.FromArgb(161, 161, 170); // #A1A1AA (Zinc 400)
    public static readonly Color TextMuted = Color.FromArgb(113, 113, 122);     // #71717A (Zinc 500)
}
