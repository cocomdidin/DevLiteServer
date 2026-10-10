using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DevLiteServer.Core;

public static class SqlServerDriverManager
{
    public const string MsOdbcDownloadUrl = "https://download.microsoft.com/download/e/a/c/eac5a469-aa30-4fd5-a37b-508f6a2a0590/msodbcsql.msi";

    public static bool IsOdbcDriverInstalled(out string? driverName)
    {
        driverName = null;
        try
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var odbcKey = baseKey.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers");
                if (odbcKey != null)
                {
                    foreach (var name in odbcKey.GetValueNames())
                    {
                        if (name.Contains("ODBC Driver", StringComparison.OrdinalIgnoreCase) &&
                            name.Contains("for SQL Server", StringComparison.OrdinalIgnoreCase))
                        {
                            driverName = name;
                            return true;
                        }
                    }
                }
            }
        }
        catch { }
        return false;
    }

    public static bool IsPhpDriverInstalled(string phpDir)
    {
        if (string.IsNullOrWhiteSpace(phpDir) || !Directory.Exists(phpDir)) return false;

        string extDir = Path.Combine(phpDir, "ext");
        bool hasPdo = File.Exists(Path.Combine(extDir, "php_pdo_sqlsrv.dll"));
        bool hasSrv = File.Exists(Path.Combine(extDir, "php_sqlsrv.dll"));
        if (!hasPdo && !hasSrv) return false;

        string iniPath = Path.Combine(phpDir, "php.ini");
        if (File.Exists(iniPath))
        {
            string content = File.ReadAllText(iniPath);
            return Regex.IsMatch(content, @"(?m)^extension\s*=\s*(php_)?(pdo_)?sqlsrv");
        }

        return false;
    }

    public static async Task<(bool success, string message)> InstallPhpDriversAsync(
        string phpDir,
        string versionFolder,
        string tempDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            string extDir = Path.Combine(phpDir, "ext");
            if (!Directory.Exists(extDir)) Directory.CreateDirectory(extDir);

            string? zipUrl = null;
            string? pdoEntry = null;
            string? srvEntry = null;

            if (versionFolder.Contains("8.5", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = "https://github.com/microsoft/msphpsql/releases/download/v5.13.3/Windows_5.13.3RTW.zip";
                pdoEntry = "Windows/php_pdo_sqlsrv_85_nts_x64.dll";
                srvEntry = "Windows/php_sqlsrv_85_nts_x64.dll";
            }
            else if (versionFolder.Contains("8.4", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = "https://github.com/microsoft/msphpsql/releases/download/v5.13.3/Windows_5.13.3RTW.zip";
                pdoEntry = "Windows/php_pdo_sqlsrv_84_nts_x64.dll";
                srvEntry = "Windows/php_sqlsrv_84_nts_x64.dll";
            }
            else if (versionFolder.Contains("8.3", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = "https://github.com/microsoft/msphpsql/releases/download/v5.13.3/Windows_5.13.3RTW.zip";
                pdoEntry = "Windows/php_pdo_sqlsrv_83_nts_x64.dll";
                srvEntry = "Windows/php_sqlsrv_83_nts_x64.dll";
            }
            else if (versionFolder.Contains("8.2", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = "https://github.com/microsoft/msphpsql/releases/download/v5.12.0/Windows_5.12.0RTW.zip";
                pdoEntry = "Windows/php_pdo_sqlsrv_82_nts_x64.dll";
                srvEntry = "Windows/php_sqlsrv_82_nts_x64.dll";
            }
            else if (versionFolder.Contains("8.1", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = "https://github.com/microsoft/msphpsql/releases/download/v5.12.0/Windows_5.12.0RTW.zip";
                pdoEntry = "Windows/php_pdo_sqlsrv_81_nts_x64.dll";
                srvEntry = "Windows/php_sqlsrv_81_nts_x64.dll";
            }
            else if (versionFolder.Contains("7.4", StringComparison.OrdinalIgnoreCase))
            {
                zipUrl = "https://github.com/microsoft/msphpsql/releases/download/v5.10.1/Windows-7.4.zip";
                pdoEntry = "Windows-7.4/x64/php_pdo_sqlsrv_74_nts.dll";
                srvEntry = "Windows-7.4/x64/php_sqlsrv_74_nts.dll";
            }

            if (string.IsNullOrEmpty(zipUrl) || string.IsNullOrEmpty(pdoEntry) || string.IsNullOrEmpty(srvEntry))
            {
                return (false, $"Versi {versionFolder} tidak memiliki paket driver Microsoft SQL Server otomatis.");
            }

            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            string zipFile = Path.Combine(tempDir, $"msphpsql_{Guid.NewGuid():N}.zip");

            progress?.Report("Mengunduh Microsoft SQL Server driver untuk PHP...");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
            {
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DevLiteServer/1.0");
                using var resp = await http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                await using var remoteStream = await resp.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(zipFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await remoteStream.CopyToAsync(fileStream, ct);
            }

            progress?.Report("Mengekstrak file DLL ke folder ext/...");

            using (var archive = ZipFile.OpenRead(zipFile))
            {
                var entryPdo = archive.GetEntry(pdoEntry);
                var entrySrv = archive.GetEntry(srvEntry);

                entryPdo ??= archive.Entries.FirstOrDefault(e => e.Name.Contains("pdo_sqlsrv", StringComparison.OrdinalIgnoreCase) && e.Name.Contains("nts", StringComparison.OrdinalIgnoreCase) && (e.Name.Contains("64") || e.FullName.Contains("x64")));
                entrySrv ??= archive.Entries.FirstOrDefault(e => e.Name.Contains("php_sqlsrv", StringComparison.OrdinalIgnoreCase) && e.Name.Contains("nts", StringComparison.OrdinalIgnoreCase) && (e.Name.Contains("64") || e.FullName.Contains("x64")));

                if (entryPdo != null)
                {
                    entryPdo.ExtractToFile(Path.Combine(extDir, "php_pdo_sqlsrv.dll"), true);
                }
                if (entrySrv != null)
                {
                    entrySrv.ExtractToFile(Path.Combine(extDir, "php_sqlsrv.dll"), true);
                }
            }

            try { if (File.Exists(zipFile)) File.Delete(zipFile); } catch { }

            progress?.Report("Mengaktifkan ekstensi sqlsrv dan pdo_sqlsrv di php.ini...");
            EnableInPhpIni(phpDir, true);

            return (true, "Driver PHP SQL Server berhasil diinstal dan diaktifkan.");
        }
        catch (Exception ex)
        {
            return (false, $"Gagal menginstal driver PHP SQL Server: {ex.Message}");
        }
    }

    public static bool RemovePhpDrivers(string phpDir)
    {
        try
        {
            EnableInPhpIni(phpDir, false);

            string extDir = Path.Combine(phpDir, "ext");
            string pdoDll = Path.Combine(extDir, "php_pdo_sqlsrv.dll");
            string srvDll = Path.Combine(extDir, "php_sqlsrv.dll");

            try { if (File.Exists(pdoDll)) File.Delete(pdoDll); } catch { }
            try { if (File.Exists(srvDll)) File.Delete(srvDll); } catch { }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void EnableInPhpIni(string phpDir, bool enable)
    {
        string iniPath = Path.Combine(phpDir, "php.ini");
        if (!File.Exists(iniPath)) return;

        string content = File.ReadAllText(iniPath);

        if (enable)
        {
            content = content.Replace(";extension=sqlsrv", "extension=sqlsrv");
            content = content.Replace(";extension=pdo_sqlsrv", "extension=pdo_sqlsrv");
            content = content.Replace(";extension=php_sqlsrv.dll", "extension=php_sqlsrv.dll");
            content = content.Replace(";extension=php_pdo_sqlsrv.dll", "extension=php_pdo_sqlsrv.dll");

            if (!Regex.IsMatch(content, @"(?m)^extension\s*=\s*(php_)?pdo_sqlsrv"))
            {
                content += "\r\n; Microsoft SQL Server Driver for PHP (PDO)\r\nextension=pdo_sqlsrv\r\n";
            }
            if (!Regex.IsMatch(content, @"(?m)^extension\s*=\s*(php_)?sqlsrv"))
            {
                content += "extension=sqlsrv\r\n";
            }
        }
        else
        {
            content = Regex.Replace(content, @"(?m)^extension\s*=\s*pdo_sqlsrv", ";extension=pdo_sqlsrv");
            content = Regex.Replace(content, @"(?m)^extension\s*=\s*sqlsrv", ";extension=sqlsrv");
            content = Regex.Replace(content, @"(?m)^extension\s*=\s*php_pdo_sqlsrv\.dll", ";extension=php_pdo_sqlsrv.dll");
            content = Regex.Replace(content, @"(?m)^extension\s*=\s*php_sqlsrv\.dll", ";extension=php_sqlsrv.dll");
        }

        File.WriteAllText(iniPath, content);
    }

    public static async Task<(bool success, string message)> InstallOdbcDriverAsync(
        string tempDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            if (IsOdbcDriverInstalled(out string? existingDriver))
            {
                return (true, $"{existingDriver} sudah terpasang di sistem Windows.");
            }

            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            string msiFile = Path.Combine(tempDir, "msodbcsql.msi");

            progress?.Report("Mengunduh installer Microsoft ODBC Driver 18 for SQL Server...");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) })
            {
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DevLiteServer/1.0");
                using var resp = await http.GetAsync(MsOdbcDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                await using var remoteStream = await resp.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(msiFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                await remoteStream.CopyToAsync(fileStream, ct);
            }

            progress?.Report("Menjalankan installer Microsoft ODBC Driver...");

            var psi = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{msiFile}\" /passive /norestart IACCEPTMSODBCSQLLICENSETERMS=YES",
                UseShellExecute = true
            };

            var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct);
            }

            try { if (File.Exists(msiFile)) File.Delete(msiFile); } catch { }

            if (IsOdbcDriverInstalled(out string? installedName))
            {
                return (true, $"Microsoft ODBC Driver berhasil diinstal ({installedName}).");
            }
            else
            {
                return (false, "Installer selesai, namun driver ODBC belum terdeteksi di registry sistem.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Gagal menginstal Microsoft ODBC Driver: {ex.Message}");
        }
    }
}
