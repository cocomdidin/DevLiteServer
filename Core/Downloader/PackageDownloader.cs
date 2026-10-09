using System.IO.Compression;

namespace DevLiteServer.Core.Downloader;

public record PackageDownloadProgress(
    double Percent,
    long BytesReceived,
    long TotalBytes,
    string StatusText,
    bool IsCompleted = false,
    bool HasError = false,
    string? ErrorMessage = null
);

public static class PackageDownloader
{
    public static async Task<bool> DownloadAndInstallAsync(
        PackageItem pkg,
        string appRoot,
        IProgress<PackageDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        string tempDir = Path.Combine(appRoot, "build", "temp_downloads");
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        string tempZip = Path.Combine(tempDir, $"{pkg.Id}_{Guid.NewGuid():N}.zip");
        string tempExtract = Path.Combine(tempDir, $"extract_{pkg.Id}_{Guid.NewGuid():N}");
        string targetDir = pkg.GetInstallDirectory(appRoot);

        long totalBytes = pkg.ApproximateSizeBytes;
        long bytesReceived = 0;

        try
        {
            progress?.Report(new PackageDownloadProgress(0, 0, pkg.ApproximateSizeBytes, "Connecting to official server..."));

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DevLiteServer/1.0");

                using var response = await http.GetAsync(pkg.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                totalBytes = response.Content.Headers.ContentLength ?? pkg.ApproximateSizeBytes;

                await using (var remoteStream = await response.Content.ReadAsStreamAsync(ct))
                await using (var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    var lastReport = DateTime.UtcNow;

                    while ((read = await remoteStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                        bytesReceived += read;

                        if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 120 || bytesReceived == totalBytes)
                        {
                            lastReport = DateTime.UtcNow;
                            double percent = totalBytes > 0 ? (double)bytesReceived / totalBytes * 100.0 : 0.0;
                            double mbRecv = bytesReceived / (1024.0 * 1024.0);
                            double mbTotal = totalBytes / (1024.0 * 1024.0);

                            progress?.Report(new PackageDownloadProgress(
                                percent,
                                bytesReceived,
                                totalBytes,
                                $"Downloading {pkg.Name}... {mbRecv:F1} / {mbTotal:F1} MB ({percent:F0}%)"));
                        }
                    }
                }
            }

            // Extraction phase
            progress?.Report(new PackageDownloadProgress(100, bytesReceived, totalBytes, "Extracting package contents..."));

            if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
            Directory.CreateDirectory(tempExtract);

            await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true), ct);

            // Cleanup zip immediately
            if (File.Exists(tempZip)) File.Delete(tempZip);

            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            // Folder flattening: archives often contain a single root folder (e.g. pgsql, mysql-8.4..., node-v20...)
            string[] subDirs = Directory.GetDirectories(tempExtract);
            string sourceExtractDir = tempExtract;
            if (Directory.GetFiles(tempExtract).Length == 0 && subDirs.Length == 1)
            {
                sourceExtractDir = subDirs[0];
            }
            else if (pkg.Category.Equals("Node", StringComparison.OrdinalIgnoreCase) && subDirs.Length == 1 &&
                File.Exists(Path.Combine(subDirs[0], "node.exe")))
            {
                sourceExtractDir = subDirs[0];
            }

            // Move files into targetDir
            await Task.Run(() => CopyDirectoryContents(sourceExtractDir, targetDir), ct);

            // Cleanup temp extract folder
            try { Directory.Delete(tempExtract, true); } catch { }

            // Post-install PHP php.ini configuration
            if (pkg.Category.Equals("PHP", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(new PackageDownloadProgress(100, totalBytes, totalBytes, "Configuring php.ini and extensions..."));
                ConfigurePhpIni(targetDir);
            }

            // Verification
            if (!pkg.IsInstalled(appRoot))
            {
                throw new InvalidOperationException($"Package files extracted, but executable was not found in: {targetDir}");
            }

            progress?.Report(new PackageDownloadProgress(100, totalBytes, totalBytes, $"{pkg.Name} installed successfully.", IsCompleted: true));
            return true;
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new PackageDownloadProgress(0, 0, 0, "Download cancelled.", HasError: true, ErrorMessage: "Cancelled by user."));
            CleanupSafely(tempZip, tempExtract);
            return false;
        }
        catch (Exception ex)
        {
            progress?.Report(new PackageDownloadProgress(0, 0, 0, "Installation failed.", HasError: true, ErrorMessage: ex.Message));
            CleanupSafely(tempZip, tempExtract);
            return false;
        }
    }

    public static async Task<bool> UninstallPackageAsync(PackageItem pkg, string appRoot)
    {
        string dir = pkg.GetInstallDirectory(appRoot);
        if (!Directory.Exists(dir)) return true;

        try
        {
            await Task.Run(() => Directory.Delete(dir, true));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDirectoryContents(string sourceDir, string targetDir)
    {
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string destFile = Path.Combine(targetDir, relativePath);
            string? destDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            File.Copy(file, destFile, true);
        }
    }

    private static void ConfigurePhpIni(string phpDir)
    {
        string iniTarget = Path.Combine(phpDir, "php.ini");
        if (File.Exists(iniTarget)) return;

        string iniDev = Path.Combine(phpDir, "php.ini-development");
        string iniProd = Path.Combine(phpDir, "php.ini-production");
        string? sourceIni = File.Exists(iniDev) ? iniDev : (File.Exists(iniProd) ? iniProd : null);

        if (sourceIni == null) return;

        string content = File.ReadAllText(sourceIni);

        // Uncomment extension_dir
        content = content.Replace(";extension_dir = \"ext\"", "extension_dir = \"ext\"");

        // Enable common extensions for modern web development (Laravel, WordPress, etc.)
        string[] extensions =
        [
            "curl", "fileinfo", "mbstring", "mysqli", "openssl",
            "pdo_mysql", "pdo_pgsql", "pgsql", "pdo_sqlite", "sqlite3",
            "gd", "zip"
        ];

        foreach (var ext in extensions)
        {
            content = content.Replace($";extension={ext}", $"extension={ext}");
        }

        // Tweak developer limits
        content = content.Replace("upload_max_filesize = 2M", "upload_max_filesize = 128M");
        content = content.Replace("post_max_size = 8M", "post_max_size = 128M");
        content = content.Replace("memory_limit = 128M", "memory_limit = 512M");
        content = content.Replace("max_execution_time = 30", "max_execution_time = 300");

        File.WriteAllText(iniTarget, content);
    }

    private static void CleanupSafely(string tempZip, string tempExtract)
    {
        try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
        try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true); } catch { }
    }
}
