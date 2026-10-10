using System.Runtime.InteropServices;

namespace DevLiteServer.Core;

public static class UserEnvironmentManager
{
    public static bool IsRegistered(string appRoot)
    {
        try
        {
            string? userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
            if (string.IsNullOrEmpty(userPath)) return false;

            string phpCurrent = Path.Combine(appRoot, "bin", "php", "current");
            return userPath.Contains(phpCurrent, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetRegistration(string appRoot, bool enable)
    {
        try
        {
            string? userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var paths = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            string phpCurrent = Path.Combine(appRoot, "bin", "php", "current");
            string nodeCurrent = Path.Combine(appRoot, "bin", "nodejs", "current");
            string composerPath = Path.Combine(appRoot, "tools", "composer");

            paths.RemoveAll(p => p.Equals(phpCurrent, StringComparison.OrdinalIgnoreCase) ||
                                 p.Equals(nodeCurrent, StringComparison.OrdinalIgnoreCase) ||
                                 p.Equals(composerPath, StringComparison.OrdinalIgnoreCase));

            if (enable)
            {
                paths.Insert(0, composerPath);
                paths.Insert(0, nodeCurrent);
                paths.Insert(0, phpCurrent);
            }

            string newPath = string.Join(";", paths);
            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);

            BroadcastEnvironmentChange();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private const uint HWND_BROADCAST = 0xffff;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private static void BroadcastEnvironmentChange()
    {
        try
        {
            SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 1000, out _);
        }
        catch { }
    }
}
