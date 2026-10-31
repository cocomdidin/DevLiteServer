using System.IO.Compression;
using System.Text;
using DevLiteServer.Core.Downloader;
using DevLiteServer.Services;

namespace DevLiteServer.Core;

public enum CoreComponentKind
{
    VcRedist,
    Nginx,
    Php,
    Composer,
    Git,
    Adminer
}

public class CoreComponentStatus
{
    public CoreComponentKind Kind { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ApproximateSize { get; set; } = "";
    public bool IsInstalled { get; set; }
    public string TargetPath { get; set; } = "";
}

public static class CoreProvisioner
{
    public const string NginxUrl = "https://nginx.org/download/nginx-1.26.3.zip";
    public const string Php84Url = "https://windows.php.net/downloads/releases/php-8.4.4-nts-Win32-vs17-x64.zip";
    public const string Php84FallbackUrl = "https://windows.php.net/downloads/releases/archives/php-8.4.4-nts-Win32-vs17-x64.zip";
    public const string ComposerPharUrl = "https://getcomposer.org/composer.phar";
    public const string GitUrl = "https://github.com/git-for-windows/git/releases/download/v2.56.0.windows.2/MinGit-2.56.0.2-64-bit.zip";
    public const string AdminerEvoUrl = DatabaseClientManager.AdminerEvoUrl;

    public static List<CoreComponentStatus> CheckComponents(string appRoot)
    {
        var list = new List<CoreComponentStatus>();

        // 1. MSVCRT x64 (Visual C++ 2015-2022)
        bool vcInstalled = DependencyChecker.IsVcRedistInstalled();
        list.Add(new CoreComponentStatus
        {
            Kind = CoreComponentKind.VcRedist,
            Name = "Visual C++ Runtime (MSVCRT x64)",
            Description = "Microsoft system runtime required by PHP FastCGI and database services.",
            ApproximateSize = "~24.0 MB",
            IsInstalled = vcInstalled,
            TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vcruntime140.dll")
        });

        // 2. Nginx
        string nginxDir = Path.Combine(appRoot, "bin", "nginx");
        bool nginxInstalled = File.Exists(Path.Combine(nginxDir, "nginx.exe"));
        if (!nginxInstalled && Directory.Exists(nginxDir))
        {
            nginxInstalled = Directory.GetFiles(nginxDir, "nginx.exe", SearchOption.AllDirectories).Length > 0;
        }
        list.Add(new CoreComponentStatus
        {
            Kind = CoreComponentKind.Nginx,
            Name = "Nginx 1.26",
            Description = "High-performance HTTP server & reverse proxy engine.",
            ApproximateSize = "~2.5 MB",
            IsInstalled = nginxInstalled,
            TargetPath = nginxDir
        });

        // 3. PHP 8.4
        string phpRoot = Path.Combine(appRoot, "bin", "php");
        string php84Dir = Path.Combine(phpRoot, "php-8.4");
        bool phpInstalled = File.Exists(Path.Combine(php84Dir, "php-cgi.exe")) || File.Exists(Path.Combine(phpRoot, "current", "php-cgi.exe"));
        if (!phpInstalled && Directory.Exists(phpRoot))
        {
            phpInstalled = Directory.GetFiles(phpRoot, "php-cgi.exe", SearchOption.AllDirectories).Length > 0;
        }
        list.Add(new CoreComponentStatus
        {
            Kind = CoreComponentKind.Php,
            Name = "PHP 8.4 (NTS x64)",
            Description = "FastCGI runtime pre-configured with popular Laravel extensions.",
            ApproximateSize = "~32.5 MB",
            IsInstalled = phpInstalled,
            TargetPath = php84Dir
        });

        // 4. Composer
        string composerDir = Path.Combine(appRoot, "tools", "composer");
        bool composerInstalled = File.Exists(Path.Combine(composerDir, "composer.phar")) &&
                                 File.Exists(Path.Combine(composerDir, "composer.bat"));
        list.Add(new CoreComponentStatus
        {
            Kind = CoreComponentKind.Composer,
            Name = "Composer CLI",
            Description = "PHP package and dependency manager with global wrapper.",
            ApproximateSize = "~3.0 MB",
            IsInstalled = composerInstalled,
            TargetPath = composerDir
        });

        // 5. MinGit
        string gitDir = Path.Combine(appRoot, "bin", "git");
        bool gitInstalled = File.Exists(Path.Combine(gitDir, "cmd", "git.exe")) ||
                            File.Exists(Path.Combine(gitDir, "bin", "git.exe"));
        list.Add(new CoreComponentStatus
        {
            Kind = CoreComponentKind.Git,
            Name = "MinGit Portable",
            Description = "Lightweight Git environment for repository and package installs.",
            ApproximateSize = "~25.0 MB",
            IsInstalled = gitInstalled,
            TargetPath = gitDir
        });

        // 6. AdminerEvo
        string adminerDir = Path.Combine(appRoot, "tools", "adminer");
        bool adminerInstalled = File.Exists(Path.Combine(adminerDir, "adminer.php"));
        list.Add(new CoreComponentStatus
        {
            Kind = CoreComponentKind.Adminer,
            Name = "AdminerEvo Web Client",
            Description = "Single-file database manager for MySQL, PostgreSQL, and SQLite.",
            ApproximateSize = "~500 KB",
            IsInstalled = adminerInstalled,
            TargetPath = adminerDir
        });

        return list;
    }

    public static bool NeedsSetup(string appRoot)
    {
        return CheckComponents(appRoot).Any(c => !c.IsInstalled);
    }

    public static async Task<bool> InstallComponentAsync(
        CoreComponentKind kind,
        string appRoot,
        IProgress<(double Percent, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        string tempDir = Path.Combine(appRoot, "build", "temp_downloads");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DevLiteServer/1.0");

        try
        {
            switch (kind)
            {
                case CoreComponentKind.VcRedist:
                    {
                        progress?.Report((0, "Downloading MSVCRT x64 (~24.0 MB)..."));
                        var vcProgress = new Progress<int>(pct => progress?.Report((pct, $"Downloading MSVCRT installer: {pct}%")));
                        bool ok = await DependencyChecker.InstallVcRedistAsync(vcProgress);
                        if (ok || DependencyChecker.IsVcRedistInstalled())
                        {
                            progress?.Report((100, "Visual C++ Runtime installed."));
                            return true;
                        }
                        return false;
                    }

                case CoreComponentKind.Nginx:
                    {
                        string targetDir = Path.Combine(appRoot, "bin", "nginx");
                        string zipPath = Path.Combine(tempDir, "nginx.zip");
                        string extractDir = Path.Combine(tempDir, "extract_nginx");

                        progress?.Report((0, "Downloading Nginx 1.26 (~2.5 MB)..."));
                        await DownloadWithProgressAsync(http, NginxUrl, null, zipPath, progress, ct);

                        progress?.Report((100, "Extracting Nginx..."));
                        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                        Directory.CreateDirectory(extractDir);
                        ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        string[] subDirs = Directory.GetDirectories(extractDir);
                        if (subDirs.Length == 1 && Directory.GetFiles(extractDir).Length == 0)
                        {
                            string subName = Path.GetFileName(subDirs[0]);
                            string destSub = Path.Combine(targetDir, subName);
                            if (Directory.Exists(destSub)) Directory.Delete(destSub, true);
                            Directory.Move(subDirs[0], destSub);
                        }
                        else
                        {
                            CopyDirectoryRecursive(extractDir, targetDir);
                        }

                        CleanupSafely(zipPath, extractDir);
                        progress?.Report((100, "Nginx installed."));
                        return true;
                    }

                case CoreComponentKind.Php:
                    {
                        string targetDir = Path.Combine(appRoot, "bin", "php", "php-8.4");
                        string zipPath = Path.Combine(tempDir, "php84.zip");
                        string extractDir = Path.Combine(tempDir, "extract_php84");

                        progress?.Report((0, "Downloading PHP 8.4 (~32.5 MB)..."));
                        await DownloadWithProgressAsync(http, Php84Url, Php84FallbackUrl, zipPath, progress, ct);

                        progress?.Report((100, "Extracting PHP 8.4..."));
                        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                        Directory.CreateDirectory(extractDir);
                        ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                        Directory.CreateDirectory(targetDir);

                        string[] subDirs = Directory.GetDirectories(extractDir);
                        string sourceDir = (subDirs.Length == 1 && Directory.GetFiles(extractDir).Length == 0) ? subDirs[0] : extractDir;
                        CopyDirectoryRecursive(sourceDir, targetDir);

                        progress?.Report((100, "Configuring php.ini and extensions..."));
                        PackageDownloader.ConfigurePhpIni(targetDir);
                        PhpService.UpdateCurrentJunction(appRoot, "php-8.4");

                        CleanupSafely(zipPath, extractDir);
                        progress?.Report((100, "PHP 8.4 installed and ready."));
                        return true;
                    }

                case CoreComponentKind.Composer:
                    {
                        string targetDir = Path.Combine(appRoot, "tools", "composer");
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        string pharPath = Path.Combine(targetDir, "composer.phar");
                        string batPath = Path.Combine(targetDir, "composer.bat");

                        progress?.Report((0, "Downloading Composer (~3.0 MB)..."));
                        await DownloadWithProgressAsync(http, ComposerPharUrl, null, pharPath, progress, ct);

                        progress?.Report((100, "Creating composer.bat CLI wrapper..."));
                        string batContent = "@echo off\r\nphp \"%~dp0composer.phar\" %*\r\n";
                        await File.WriteAllTextAsync(batPath, batContent, Encoding.UTF8, ct);

                        progress?.Report((100, "Composer installed."));
                        return true;
                    }

                case CoreComponentKind.Git:
                    {
                        string targetDir = Path.Combine(appRoot, "bin", "git");
                        string zipPath = Path.Combine(tempDir, "mingit.zip");
                        string extractDir = Path.Combine(tempDir, "extract_mingit");

                        progress?.Report((0, "Downloading MinGit (~25.0 MB)..."));
                        await DownloadWithProgressAsync(http, GitUrl, null, zipPath, progress, ct);

                        progress?.Report((100, "Extracting MinGit..."));
                        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                        Directory.CreateDirectory(extractDir);
                        ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                        Directory.CreateDirectory(targetDir);

                        CopyDirectoryRecursive(extractDir, targetDir);

                        CleanupSafely(zipPath, extractDir);
                        progress?.Report((100, "MinGit installed."));
                        return true;
                    }

                case CoreComponentKind.Adminer:
                    {
                        string targetDir = Path.Combine(appRoot, "tools", "adminer");
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        string adminerPhp = Path.Combine(targetDir, "adminer.php");

                        progress?.Report((0, "Downloading AdminerEvo (~500 KB)..."));
                        await DownloadWithProgressAsync(http, AdminerEvoUrl, null, adminerPhp, progress, ct);

                        progress?.Report((100, "AdminerEvo installed."));
                        return true;
                    }
            }
        }
        catch (Exception ex)
        {
            progress?.Report((0, $"Error: {ex.Message}"));
            return false;
        }

        return false;
    }

    public static void EnsureStarterWebRoot(string appRoot)
    {
        string wwwDir = Path.Combine(appRoot, "www");
        if (!Directory.Exists(wwwDir)) Directory.CreateDirectory(wwwDir);

        string indexPhp = Path.Combine(wwwDir, "index.php");
        if (!File.Exists(indexPhp))
        {
            string content = """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Dev Lite Server - Local Environment</title>
                <link rel="icon" type="image/x-icon" href="/favicon.ico">
                <style>
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                        background: #0f172a;
                        color: #f8fafc;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        min-height: 100vh;
                        margin: 0;
                    }
                    .card {
                        background: #1e293b;
                        padding: 2.5rem;
                        border-radius: 12px;
                        box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.3);
                        max-width: 540px;
                        width: 100%;
                        border: 1px solid #334155;
                    }
                    h1 {
                        margin-top: 0;
                        color: #38bdf8;
                        font-size: 1.75rem;
                    }
                    p {
                        color: #94a3b8;
                        line-height: 1.6;
                    }
                    .badge {
                        display: inline-block;
                        background: #0369a1;
                        color: #e0f2fe;
                        padding: 0.25rem 0.6rem;
                        border-radius: 9999px;
                        font-size: 0.85rem;
                        font-weight: 600;
                    }
                    .grid {
                        margin-top: 1.5rem;
                        border-top: 1px solid #334155;
                        padding-top: 1.25rem;
                        display: grid;
                        grid-template-columns: 1fr 1fr;
                        gap: 1rem;
                    }
                    .stat {
                        background: #0f172a;
                        padding: 0.75rem 1rem;
                        border-radius: 8px;
                        border: 1px solid #1e293b;
                    }
                    .stat-label {
                        font-size: 0.75rem;
                        text-transform: uppercase;
                        color: #64748b;
                        font-weight: 700;
                    }
                    .stat-val {
                        margin-top: 0.25rem;
                        font-size: 1.1rem;
                        color: #f1f5f9;
                        font-weight: 600;
                    }
                    .links {
                        margin-top: 1.5rem;
                        display: flex;
                        gap: 0.75rem;
                    }
                    .btn {
                        display: inline-block;
                        background: #2563eb;
                        color: white;
                        text-decoration: none;
                        padding: 0.5rem 1rem;
                        border-radius: 6px;
                        font-weight: 500;
                        font-size: 0.9rem;
                        transition: background 0.2s;
                    }
                    .btn:hover {
                        background: #1d4ed8;
                    }
                    .btn-secondary {
                        background: #334155;
                    }
                    .btn-secondary:hover {
                        background: #475569;
                    }
                </style>
            </head>
            <body>
                <div class="card">
                    <span class="badge">Dev Lite Server Running</span>
                    <h1>Welcome to Dev Lite Server</h1>
                    <p>Your portable local web development environment is running smoothly.</p>

                    <div class="grid">
                        <div class="stat">
                            <div class="stat-label">PHP Version</div>
                            <div class="stat-val"><?php echo PHP_VERSION; ?></div>
                        </div>
                        <div class="stat">
                            <div class="stat-label">Web Server</div>
                            <div class="stat-val"><?php echo $_SERVER['SERVER_SOFTWARE'] ?? 'Nginx'; ?></div>
                        </div>
                        <div class="stat">
                            <div class="stat-label">Server Port</div>
                            <div class="stat-val"><?php echo $_SERVER['SERVER_PORT'] ?? '80'; ?></div>
                        </div>
                        <div class="stat">
                            <div class="stat-label">Document Root</div>
                            <div class="stat-val" style="font-size: 0.85rem; word-break: break-all;"><?php echo __DIR__; ?></div>
                        </div>
                    </div>

                    <div class="links">
                        <a href="/adminer" class="btn">Database Manager (Adminer)</a>
                        <a href="http://localhost:8025" target="_blank" class="btn btn-secondary">Mailpit Web (:8025)</a>
                    </div>
                </div>
            </body>
            </html>
            """;
            try
            {
                File.WriteAllText(indexPhp, content, Encoding.UTF8);
            }
            catch { }
        }
    }

    private static async Task DownloadWithProgressAsync(
        HttpClient http,
        string url,
        string? fallbackUrl,
        string destFile,
        IProgress<(double Percent, string Status)>? progress,
        CancellationToken ct)
    {
        string currentUrl = url;
        HttpResponseMessage? response = null;

        try
        {
            response = await http.GetAsync(currentUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode && !string.IsNullOrEmpty(fallbackUrl))
            {
                response.Dispose();
                currentUrl = fallbackUrl;
                response = await http.GetAsync(currentUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            response.EnsureSuccessStatusCode();
        }
        catch when (!string.IsNullOrEmpty(fallbackUrl) && currentUrl != fallbackUrl)
        {
            response?.Dispose();
            currentUrl = fallbackUrl;
            response = await http.GetAsync(currentUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }

        using (response)
        {
            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            long bytesReceived = 0;

            await using var remoteStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            int read;
            var lastReport = DateTime.UtcNow;

            while ((read = await remoteStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesReceived += read;

                if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 150 || bytesReceived == totalBytes)
                {
                    lastReport = DateTime.UtcNow;
                    double percent = totalBytes > 0 ? (double)bytesReceived / totalBytes * 100.0 : 0.0;
                    double mbRecv = bytesReceived / (1024.0 * 1024.0);
                    double mbTotal = totalBytes / (1024.0 * 1024.0);

                    string status = totalBytes > 0
                        ? $"Downloading... {mbRecv:F1} / {mbTotal:F1} MB ({percent:F0}%)"
                        : $"Downloading... {mbRecv:F1} MB";

                    progress?.Report((percent, status));
                }
            }
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, targetSubDir);
        }
    }

    private static void CleanupSafely(string zipPath, string extractDir)
    {
        try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
        try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
    }
}
