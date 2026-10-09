using DevLiteServer.Core;

namespace DevLiteServer;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (!SingleInstance.TryAcquire(out var mutex))
        {
            // Another instance is already running (e.g. in tray).
            // Signal it to restore to foreground and exit this instance immediately.
            SingleInstance.SignalFirstInstance();
            return;
        }

        bool startMinimized = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                                            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                            a.Equals("-tray", StringComparison.OrdinalIgnoreCase));

        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.System);
            Application.Run(new MainForm(startMinimized));
        }
        finally
        {
            mutex?.ReleaseMutex();
            mutex?.Dispose();
        }
    }
}
