using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DevLiteServer.Services;

namespace DevLiteServer.Core;

public enum DbClientType
{
    HeidiSql,
    PhpMyAdmin,
    AdminerEvo,
    TinyRdm,
    RedisCli,
    SystemCustom
}

public class DbClientInfo
{
    public DbClientType Type { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string ApproximateSize { get; set; } = "";
    public bool IsInstalled { get; set; }
    public string ExecutableOrPath { get; set; } = "";
}

public static class DatabaseClientManager
{
    public const string AdminerEvoUrl = "https://github.com/adminerevo/adminerevo/releases/download/v4.8.4/adminer-4.8.4.php";
    public const string HeidiSqlUrl = "https://www.heidisql.com/downloads/releases/HeidiSQL_12.10_64_Portable.zip";
    public const string PhpMyAdminUrl = "https://files.phpmyadmin.net/phpMyAdmin/5.2.2/phpMyAdmin-5.2.2-all-languages.zip";
    public const string TinyRdmUrl = "https://github.com/tiny-craft/tiny-rdm/releases/download/v1.2.7/TinyRDM_Portable_1.2.7_windows_x64.zip";

    public static List<DbClientInfo> GetClientsForService(string serviceKey, string appRoot)
    {
        var list = new List<DbClientInfo>();
        string key = serviceKey.ToLowerInvariant();

        if (key.Contains("mysql"))
        {
            list.Add(GetHeidiSqlInfo(appRoot));
            list.Add(GetPhpMyAdminInfo(appRoot));
            list.Add(GetAdminerEvoInfo(appRoot));

            var sys = FindSystemClient();
            if (sys != null && !sys.Name.Contains("HeidiSQL", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(sys);
            }
        }
        else if (key.Contains("postgre"))
        {
            list.Add(GetHeidiSqlInfo(appRoot));
            list.Add(GetAdminerEvoInfo(appRoot));

            var sys = FindSystemClient();
            if (sys != null && !sys.Name.Contains("HeidiSQL", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(sys);
            }
        }
        else if (key.Contains("redis"))
        {
            list.Add(GetTinyRdmInfo(appRoot));
            list.Add(GetRedisCliInfo(appRoot));

            var sys = FindRedisSystemClient();
            if (sys != null)
            {
                list.Add(sys);
            }
        }

        return list;
    }

    public static DbClientInfo GetHeidiSqlInfo(string appRoot)
    {
        string localExe = Path.Combine(appRoot, "tools", "heidisql", "heidisql.exe");
        string programFilesExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HeidiSQL", "heidisql.exe");

        bool installed = File.Exists(localExe) || File.Exists(programFilesExe);
        string path = File.Exists(localExe) ? localExe : (File.Exists(programFilesExe) ? programFilesExe : localExe);

        return new DbClientInfo
        {
            Type = DbClientType.HeidiSql,
            Id = "heidisql",
            Name = "HeidiSQL Portable",
            Category = "Desktop GUI • Native",
            Description = "Direct TCP connection (MySQL & PostgreSQL). Fast, lightweight, zero web dependency.",
            ApproximateSize = "~28 MB",
            IsInstalled = installed,
            ExecutableOrPath = path
        };
    }

    public static DbClientInfo GetPhpMyAdminInfo(string appRoot)
    {
        string indexPhp = Path.Combine(appRoot, "tools", "phpmyadmin", "index.php");
        bool installed = File.Exists(indexPhp);

        return new DbClientInfo
        {
            Type = DbClientType.PhpMyAdmin,
            Id = "phpmyadmin",
            Name = "phpMyAdmin",
            Category = "Web UI • MySQL Classic",
            Description = "The standard visual web interface beloved by developers from cPanel & XAMPP.",
            ApproximateSize = "~15 MB",
            IsInstalled = installed,
            ExecutableOrPath = indexPhp
        };
    }

    public static DbClientInfo GetAdminerEvoInfo(string appRoot)
    {
        string adminerPhp = Path.Combine(appRoot, "tools", "adminer", "adminer.php");
        bool installed = File.Exists(adminerPhp);

        return new DbClientInfo
        {
            Type = DbClientType.AdminerEvo,
            Id = "adminer",
            Name = "AdminerEvo",
            Category = "Web UI • Multi-Engine",
            Description = "Ultra-lightweight single-file manager for MySQL, PostgreSQL, and SQLite. PHP 8.4 ready.",
            ApproximateSize = "~500 KB",
            IsInstalled = installed,
            ExecutableOrPath = adminerPhp
        };
    }

    public static DbClientInfo GetTinyRdmInfo(string appRoot)
    {
        string dir = Path.Combine(appRoot, "tools", "tinyrdm");
        string exe = Path.Combine(dir, "TinyRDM.exe");
        bool installed = File.Exists(exe);
        if (!installed && Directory.Exists(dir))
        {
            var exes = Directory.GetFiles(dir, "*rdm*.exe", SearchOption.AllDirectories);
            if (exes.Length > 0)
            {
                exe = exes[0];
                installed = true;
            }
        }

        return new DbClientInfo
        {
            Type = DbClientType.TinyRdm,
            Id = "tinyrdm",
            Name = "Tiny RDM Portable",
            Category = "Desktop GUI • Redis",
            Description = "Visual tree key viewer, JSON formatting, TTL inspector, CLI & slow logs analyzer.",
            ApproximateSize = "~11 MB",
            IsInstalled = installed,
            ExecutableOrPath = exe
        };
    }

    public static DbClientInfo GetRedisCliInfo(string appRoot)
    {
        string redisDir = Path.Combine(appRoot, "bin", "redis");
        string cliExe = Path.Combine(redisDir, "redis-cli.exe");
        if (!File.Exists(cliExe) && Directory.Exists(redisDir))
        {
            var exes = Directory.GetFiles(redisDir, "redis-cli.exe", SearchOption.AllDirectories);
            if (exes.Length > 0) cliExe = exes[0];
        }

        bool installed = File.Exists(cliExe);

        return new DbClientInfo
        {
            Type = DbClientType.RedisCli,
            Id = "cli",
            Name = "redis-cli (Interactive Terminal)",
            Category = "CLI Console • Built-in",
            Description = "Direct loopback terminal console connected to 127.0.0.1:6379 for fast Redis commands.",
            ApproximateSize = "Built-in",
            IsInstalled = installed,
            ExecutableOrPath = cliExe
        };
    }

    public static DbClientInfo? FindSystemClient()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string[] candidates =
        [
            Path.Combine(programFiles, "HeidiSQL", "heidisql.exe"),
            Path.Combine(localAppData, "Programs", "DBeaver", "dbeaver.exe"),
            Path.Combine(programFiles, "DBeaver", "dbeaver.exe"),
            Path.Combine(programFiles, "TablePlus", "TablePlus.exe"),
            Path.Combine(localAppData, "Programs", "TablePlus", "TablePlus.exe"),
            Path.Combine(programFiles, "Beekeeper Studio", "Beekeeper Studio.exe"),
            Path.Combine(localAppData, "Programs", "Beekeeper Studio", "Beekeeper Studio.exe")
        ];

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                string name = Path.GetFileNameWithoutExtension(c);
                return new DbClientInfo
                {
                    Type = DbClientType.SystemCustom,
                    Id = "system-custom",
                    Name = $"{name} (Windows System)",
                    Category = "System Installed Desktop GUI",
                    Description = $"Auto-detected existing database tool installed on your Windows machine ({c}).",
                    ApproximateSize = "0 MB",
                    IsInstalled = true,
                    ExecutableOrPath = c
                };
            }
        }

        return null;
    }

    public static DbClientInfo? FindRedisSystemClient()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string[] candidates =
        [
            Path.Combine(programFiles, "RedisInsight", "RedisInsight.exe"),
            Path.Combine(localAppData, "Programs", "RedisInsight", "RedisInsight.exe"),
            Path.Combine(programFiles, "AnotherRedisDesktopManager", "AnotherRedisDesktopManager.exe"),
            Path.Combine(localAppData, "Programs", "AnotherRedisDesktopManager", "AnotherRedisDesktopManager.exe")
        ];

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                string name = Path.GetFileNameWithoutExtension(c);
                return new DbClientInfo
                {
                    Type = DbClientType.SystemCustom,
                    Id = "system-redis",
                    Name = $"{name} (Windows System)",
                    Category = "System Installed Redis GUI",
                    Description = $"Auto-detected Redis manager on your Windows machine ({c}).",
                    ApproximateSize = "0 MB",
                    IsInstalled = true,
                    ExecutableOrPath = c
                };
            }
        }

        return null;
    }

    public static async Task<bool> DownloadAndInstallClientAsync(
        DbClientType type,
        string appRoot,
        AppConfig config,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        string toolsDir = Path.Combine(appRoot, "tools");
        if (!Directory.Exists(toolsDir)) Directory.CreateDirectory(toolsDir);

        string tempDir = Path.Combine(appRoot, "build", "temp_downloads");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DevLiteServer/1.0");

            switch (type)
            {
                case DbClientType.AdminerEvo:
                    {
                        string adminerDir = Path.Combine(toolsDir, "adminer");
                        if (!Directory.Exists(adminerDir)) Directory.CreateDirectory(adminerDir);
                        string adminerPhp = Path.Combine(adminerDir, "adminer.php");

                        progress?.Report("Downloading AdminerEvo (~500 KB)...");
                        byte[] data = await http.GetByteArrayAsync(AdminerEvoUrl, ct);
                        await File.WriteAllBytesAsync(adminerPhp, data, ct);
                        progress?.Report("AdminerEvo installed.");
                        return true;
                    }

                case DbClientType.HeidiSql:
                    {
                        string destDir = Path.Combine(toolsDir, "heidisql");
                        string tempZip = Path.Combine(tempDir, "heidisql.zip");

                        progress?.Report("Downloading HeidiSQL Portable (~28 MB)...");
                        await DownloadFileWithProgressAsync(http, HeidiSqlUrl, tempZip, progress, ct);

                        progress?.Report("Extracting HeidiSQL Portable...");
                        if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                        Directory.CreateDirectory(destDir);

                        ZipFile.ExtractToDirectory(tempZip, destDir, true);
                        try { File.Delete(tempZip); } catch { }

                        progress?.Report("HeidiSQL Portable installed.");
                        return true;
                    }

                case DbClientType.PhpMyAdmin:
                    {
                        string destDir = Path.Combine(toolsDir, "phpmyadmin");
                        string tempZip = Path.Combine(tempDir, "phpmyadmin.zip");
                        string tempExtract = Path.Combine(tempDir, "extract_pma");

                        progress?.Report("Downloading phpMyAdmin (~15 MB)...");
                        await DownloadFileWithProgressAsync(http, PhpMyAdminUrl, tempZip, progress, ct);

                        progress?.Report("Extracting phpMyAdmin...");
                        if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                        Directory.CreateDirectory(tempExtract);

                        ZipFile.ExtractToDirectory(tempZip, tempExtract, true);

                        // Flatten single wrapper directory if present
                        string[] subDirs = Directory.GetDirectories(tempExtract);
                        string sourceDir = (subDirs.Length == 1) ? subDirs[0] : tempExtract;

                        if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                        Directory.CreateDirectory(destDir);

                        CopyDirectoryRecursive(sourceDir, destDir);
                        try { Directory.Delete(tempExtract, true); } catch { }
                        try { File.Delete(tempZip); } catch { }

                        // Generate pre-configured config.inc.php
                        string configInc = Path.Combine(destDir, "config.inc.php");
                        string blowfishSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

                        string pmaConfig = $@"<?php

$cfg['blowfish_secret'] = '{blowfishSecret}';

$i = 0;
$i++;
$cfg['Servers'][$i]['auth_type'] = 'cookie';
$cfg['Servers'][$i]['host'] = '127.0.0.1';
$cfg['Servers'][$i]['port'] = '{config.MysqlPort}';
$cfg['Servers'][$i]['compress'] = false;
$cfg['Servers'][$i]['AllowNoPassword'] = true;

$cfg['UploadDir'] = '';
$cfg['SaveDir'] = '';
";
                        await File.WriteAllTextAsync(configInc, pmaConfig, new UTF8Encoding(false), ct);

                        // Suppress PHP 8.4 deprecation notices in vendor libs before autoloading
                        string indexPhp = Path.Combine(destDir, "index.php");
                        if (File.Exists(indexPhp))
                        {
                            string indexContent = await File.ReadAllTextAsync(indexPhp, ct);
                            if (!indexContent.Contains("E_DEPRECATED"))
                            {
                                indexContent = indexContent.Replace(
                                    "declare(strict_types=1);",
                                    "declare(strict_types=1);\n\nerror_reporting(E_ALL & ~E_DEPRECATED & ~E_USER_DEPRECATED);\nini_set('display_errors', '0');"
                                );
                                await File.WriteAllTextAsync(indexPhp, indexContent, new UTF8Encoding(false), ct);
                            }
                        }

                        progress?.Report("phpMyAdmin installed.");
                        return true;
                    }

                case DbClientType.TinyRdm:
                    {
                        string destDir = Path.Combine(toolsDir, "tinyrdm");
                        string tempZip = Path.Combine(tempDir, "tinyrdm.zip");
                        string tempExtract = Path.Combine(tempDir, "extract_rdm");

                        progress?.Report("Downloading Tiny RDM Portable (~11 MB)...");
                        await DownloadFileWithProgressAsync(http, TinyRdmUrl, tempZip, progress, ct);

                        progress?.Report("Extracting Tiny RDM Portable...");
                        if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                        Directory.CreateDirectory(tempExtract);

                        ZipFile.ExtractToDirectory(tempZip, tempExtract, true);

                        string[] subDirs = Directory.GetDirectories(tempExtract);
                        string sourceDir = (subDirs.Length == 1) ? subDirs[0] : tempExtract;

                        if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                        Directory.CreateDirectory(destDir);

                        CopyDirectoryRecursive(sourceDir, destDir);
                        try { Directory.Delete(tempExtract, true); } catch { }
                        try { File.Delete(tempZip); } catch { }

                        progress?.Report("Tiny RDM Portable installed.");
                        return true;
                    }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Error: {ex.Message}");
            return false;
        }

        return false;
    }

    private static async Task DownloadFileWithProgressAsync(
        HttpClient http,
        string url,
        string destinationFile,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? 0;
        long bytesReceived = 0;

        await using var remoteStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        byte[] buffer = new byte[81920];
        int read;
        var lastReport = DateTime.UtcNow;

        while ((read = await remoteStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesReceived += read;

            if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 250 || bytesReceived == totalBytes)
            {
                lastReport = DateTime.UtcNow;
                if (totalBytes > 0)
                {
                    double percent = (double)bytesReceived / totalBytes * 100.0;
                    double mbRecv = bytesReceived / (1024.0 * 1024.0);
                    double mbTotal = totalBytes / (1024.0 * 1024.0);
                    progress?.Report($"Downloading... {mbRecv:F1} / {mbTotal:F1} MB ({percent:F0}%)");
                }
                else
                {
                    double mbRecv = bytesReceived / (1024.0 * 1024.0);
                    progress?.Report($"Downloading... {mbRecv:F1} MB");
                }
            }
        }
    }

    public static async Task LaunchClientAsync(
        DbClientInfo client,
        string serviceKey,
        string appRoot,
        AppConfig config,
        IService nginx,
        IService php,
        Action<string>? onStatus = null)
    {
        switch (client.Type)
        {
            case DbClientType.AdminerEvo:
                {
                    await EnsureWebStackRunningAsync(nginx, php, onStatus);
                    string url = $"http://localhost:{config.HttpPort}/adminer/";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    break;
                }

            case DbClientType.PhpMyAdmin:
                {
                    await EnsureWebStackRunningAsync(nginx, php, onStatus);
                    string url = $"http://localhost:{config.HttpPort}/phpmyadmin/";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    break;
                }

            case DbClientType.HeidiSql:
            case DbClientType.TinyRdm:
            case DbClientType.SystemCustom:
                {
                    if (File.Exists(client.ExecutableOrPath))
                    {
                        Process.Start(new ProcessStartInfo(client.ExecutableOrPath) { UseShellExecute = true });
                    }
                    else
                    {
                        onStatus?.Invoke($"Executable not found: {client.ExecutableOrPath}");
                    }
                    break;
                }

            case DbClientType.RedisCli:
                {
                    OpenRedisCli(appRoot, config);
                    break;
                }
        }
    }

    public static async Task EnsureWebStackRunningAsync(IService nginx, IService php, Action<string>? onStatus)
    {
        if (nginx.Status != ServiceStatus.Running)
        {
            onStatus?.Invoke("Starting Nginx web server...");
            await nginx.StartAsync();
        }
        if (php.Status != ServiceStatus.Running)
        {
            onStatus?.Invoke("Starting PHP-CGI FastCGI worker...");
            await php.StartAsync();
        }
    }

    public static void OpenRedisCli(string appRoot, AppConfig config)
    {
        string redisDir = Path.Combine(appRoot, "bin", "redis");
        string cliExe = Path.Combine(redisDir, "redis-cli.exe");
        if (!File.Exists(cliExe) && Directory.Exists(redisDir))
        {
            var exes = Directory.GetFiles(redisDir, "redis-cli.exe", SearchOption.AllDirectories);
            if (exes.Length > 0) cliExe = exes[0];
        }

        string cmd = File.Exists(cliExe)
            ? $"\"{cliExe}\" -h 127.0.0.1 -p {config.RedisPort}"
            : $"redis-cli -h 127.0.0.1 -p {config.RedisPort}";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k \"title Redis CLI ({config.RedisPort}) & echo Connecting to Redis on 127.0.0.1:{config.RedisPort}... & {cmd}\"",
            UseShellExecute = true
        };
        Process.Start(psi);
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
}
