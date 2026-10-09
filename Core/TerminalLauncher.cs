using System.Diagnostics;

namespace DevLiteServer.Core;

public static class TerminalLauncher
{
    /// <summary>
    /// Launches an isolated PowerShell/CMD terminal with PATH pre-configured
    /// for LiteServer's portable tools (PHP, Node, Git, Composer, MySQL).
    /// Does not modify global Windows environment.
    /// </summary>
    public static void OpenTerminal(string appRoot, AppConfig config, bool usePowerShell = true)
    {
        string wwwDir = Path.Combine(appRoot, "www");
        if (!Directory.Exists(wwwDir))
        {
            Directory.CreateDirectory(wwwDir);
        }

        var pathEntries = new List<string>();

        // 1. PHP active version
        string phpDir = Path.Combine(appRoot, "bin", "php", config.ActivePhp);
        if (Directory.Exists(phpDir)) pathEntries.Add(phpDir);

        // 2. Node.js active version
        string nodeDir = Path.Combine(appRoot, "bin", "nodejs", config.ActiveNode);
        if (Directory.Exists(nodeDir)) pathEntries.Add(nodeDir);

        // 3. Git Portable
        string gitCmdDir = Path.Combine(appRoot, "bin", "git", "cmd");
        if (Directory.Exists(gitCmdDir)) pathEntries.Add(gitCmdDir);

        // 4. Composer
        string composerDir = Path.Combine(appRoot, "tools", "composer");
        if (Directory.Exists(composerDir)) pathEntries.Add(composerDir);

        // 5. MySQL client binaries
        string mysqlDir = Path.Combine(appRoot, "bin", "mysql");
        if (Directory.Exists(mysqlDir))
        {
            var subDirs = Directory.GetDirectories(mysqlDir);
            string targetDir = subDirs.Length > 0 ? subDirs[0] : mysqlDir;
            string mysqlBin = Path.Combine(targetDir, "bin");
            if (Directory.Exists(mysqlBin)) pathEntries.Add(mysqlBin);
        }

        // Combine with system PATH
        string systemPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        string injectedPath = string.Join(";", pathEntries) + ";" + systemPath;

        var psi = new ProcessStartInfo
        {
            FileName = usePowerShell ? "powershell.exe" : "cmd.exe",
            WorkingDirectory = wwwDir,
            UseShellExecute = false
        };

        psi.EnvironmentVariables["PATH"] = injectedPath;

        if (usePowerShell)
        {
            psi.Arguments = "-NoExit -Command \"Write-Host 'Dev Lite Server Isolated Terminal' -ForegroundColor Cyan; Write-Host 'Environment: PHP, Node, Git, Composer, MySQL loaded.' -ForegroundColor Gray\"";
        }

        Process.Start(psi);
    }
}
