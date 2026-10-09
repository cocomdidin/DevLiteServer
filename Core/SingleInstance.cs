using System.Runtime.InteropServices;

namespace DevLiteServer.Core;

/// <summary>
/// Enforces single-instance application lifecycle.
/// If an instance is already running (e.g. minimized to system tray),
/// secondary launches broadcast a custom Win32 message to restore and focus
/// the primary instance before exiting immediately.
/// </summary>
public static class SingleInstance
{
    private const string MutexName = @"Local\DevLiteServer_SingleInstance_Mutex_9D42C1";
    public const string WindowMessageName = "DevLiteServer_ShowWindow_Message_9D42C1";

    public static readonly int WmShowFirstInstance;

    public static bool IsRestoreMessage(int msg) => WmShowFirstInstance != 0 && msg == WmShowFirstInstance;

    private static readonly IntPtr HwndBroadcast = (IntPtr)0xffff;
    private const int SwRestore = 9;

    static SingleInstance()
    {
        WmShowFirstInstance = (int)RegisterWindowMessage(WindowMessageName);
    }

    public static bool TryAcquire(out Mutex? acquiredMutex)
    {
        var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            acquiredMutex = null;
            return false;
        }

        acquiredMutex = mutex;
        return true;
    }

    public static void SignalFirstInstance()
    {
        if (WmShowFirstInstance != 0)
        {
            PostMessage(HwndBroadcast, (uint)WmShowFirstInstance, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void ForceForeground(IntPtr hWnd)
    {
        ShowWindow(hWnd, SwRestore);
        SetForegroundWindow(hWnd);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
