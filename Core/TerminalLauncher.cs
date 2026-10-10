using System.Diagnostics;
using DevLiteServer.Services;

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

        // 1. PHP active version (via NTFS junction for dynamic switching across running terminals)
        PhpService.UpdateCurrentJunction(appRoot, config.ActivePhp);
        string currentJunction = Path.Combine(appRoot, "bin", "php", "current");
        if (Directory.Exists(currentJunction))
        {
            pathEntries.Add(currentJunction);
        }
        else
        {
            string phpDir = Path.Combine(appRoot, "bin", "php", config.ActivePhp);
            if (Directory.Exists(phpDir)) pathEntries.Add(phpDir);
        }

        // 2. Node.js active version (via NTFS junction for dynamic switching across running terminals)
        NodeManager.UpdateCurrentJunction(appRoot, config.ActiveNode);
        string currentNodeJunction = Path.Combine(appRoot, "bin", "nodejs", "current");
        if (Directory.Exists(currentNodeJunction))
        {
            pathEntries.Add(currentNodeJunction);
        }
        else
        {
            string nodeDir = Path.Combine(appRoot, "bin", "nodejs", config.ActiveNode);
            if (Directory.Exists(nodeDir)) pathEntries.Add(nodeDir);
        }

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

        // 6. PostgreSQL client binaries
        string pgDir = Path.Combine(appRoot, "bin", "postgresql");
        if (Directory.Exists(pgDir))
        {
            var subDirs = Directory.GetDirectories(pgDir);
            string targetDir = subDirs.Length > 0 ? subDirs[0] : pgDir;
            string pgBin = Path.Combine(targetDir, "bin");
            if (Directory.Exists(pgBin)) pathEntries.Add(pgBin);
            else if (Directory.Exists(Path.Combine(pgDir, "bin"))) pathEntries.Add(Path.Combine(pgDir, "bin"));
        }

        // 7. Redis CLI binary
        string redisDir = Path.Combine(appRoot, "bin", "redis");
        if (Directory.Exists(redisDir))
        {
            var subDirs = Directory.GetDirectories(redisDir);
            string targetDir = subDirs.Length > 0 ? subDirs[0] : redisDir;
            pathEntries.Add(targetDir);
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
            psi.Arguments = "-NoExit -Command \"Write-Host 'Dev Lite Server Isolated Terminal' -ForegroundColor Cyan; Write-Host 'Environment: PHP, Node, Git, Composer, MySQL, PostgreSQL, Redis loaded.' -ForegroundColor Gray\"";
        }

        Process.Start(psi);
    }
}
