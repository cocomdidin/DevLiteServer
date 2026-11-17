using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DevLiteServer.Core;

public class SiteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public string PhysicalPath { get; set; } = "";
    public string DocumentRoot { get; set; } = "";
    public string PhpVersion { get; set; } = "default"; // "default", "php-8.4", "php-8.2", etc.
    public bool SslEnabled { get; set; } = false;
    public bool IsExternal { get; set; } = false;

    // Site Type: "php" or "proxy" (for Node.js, Next.js, Nuxt, Vite, etc.)
    public string SiteType { get; set; } = "php";
    public int ProxyPort { get; set; } = 3000;

    [JsonIgnore]
    public bool IsProxy => string.Equals(SiteType, "proxy", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool HasPublicSubfolder =>
        !IsProxy &&
        !string.IsNullOrEmpty(DocumentRoot) &&
        !DocumentRoot.Equals(PhysicalPath, StringComparison.OrdinalIgnoreCase);
}

public static class VirtualHostManager
{
    private const string HostsMarkerStart = "# DEV LITE SERVER VHOSTS START";
    private const string HostsMarkerEnd = "# DEV LITE SERVER VHOSTS END";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string GetSitesJsonPath(string appRoot) => Path.Combine(appRoot, "sites.json");

    public static List<SiteItem> DetectVirtualHosts(string appRoot) => GetAllSites(appRoot);

    /// <summary>
    /// Loads all configured sites, merging discovered /www folders with external sites and custom settings.
    /// </summary>
    public static List<SiteItem> GetAllSites(string appRoot)
    {
        var savedSites = LoadSites(appRoot);
        var resultList = new List<SiteItem>();
        bool changed = false;

        // 1. Preserve external sites
        foreach (var site in savedSites.Where(s => s.IsExternal))
        {
            resultList.Add(site);
        }

        // 2. Discover sites in /www
        string wwwDir = Path.Combine(appRoot, "www");
        if (Directory.Exists(wwwDir))
        {
            foreach (var dir in Directory.GetDirectories(wwwDir))
            {
                string folderName = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(folderName) || folderName.StartsWith('.')) continue;

                string normalizedDir = Path.GetFullPath(dir);
                var existing = savedSites.FirstOrDefault(s =>
                    !s.IsExternal &&
                    (string.Equals(Path.GetFullPath(s.PhysicalPath), normalizedDir, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(s.Name, folderName, StringComparison.OrdinalIgnoreCase)));

                if (existing != null)
                {
                    existing.PhysicalPath = normalizedDir;
                    if (string.IsNullOrEmpty(existing.DocumentRoot) || !Directory.Exists(existing.DocumentRoot))
                    {
                        existing.DocumentRoot = DetectDocumentRoot(normalizedDir);
                        changed = true;
                    }
                    resultList.Add(existing);
                }
                else
                {
                    // New discovered project in /www
                    string domain = $"{folderName.ToLowerInvariant()}.test";
                    var (detectedType, detectedDocRoot, proxyPort) = DetectProjectType(normalizedDir);

                    var newSite = new SiteItem
                    {
                        Name = folderName,
                        Domain = domain,
                        PhysicalPath = normalizedDir,
                        DocumentRoot = detectedDocRoot,
                        PhpVersion = "default",
                        SslEnabled = false,
                        IsExternal = false,
                        SiteType = detectedType,
                        ProxyPort = proxyPort
                    };
                    resultList.Add(newSite);
                    changed = true;
                }
            }
        }

        if (changed || !File.Exists(GetSitesJsonPath(appRoot)))
        {
            SaveSites(appRoot, resultList);
        }

        return resultList;
    }

    public static List<SiteItem> LoadSites(string appRoot)
    {
        string jsonPath = GetSitesJsonPath(appRoot);
        if (!File.Exists(jsonPath)) return [];

        try
        {
            string json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<List<SiteItem>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveSites(string appRoot, List<SiteItem> sites)
    {
        try
        {
            string jsonPath = GetSitesJsonPath(appRoot);
            string json = JsonSerializer.Serialize(sites, JsonOptions);
            File.WriteAllText(jsonPath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    public static void AddOrUpdateSite(string appRoot, SiteItem site)
    {
        var sites = GetAllSites(appRoot);
        int index = sites.FindIndex(s => s.Id == site.Id || string.Equals(s.Domain, site.Domain, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            sites[index] = site;
        }
        else
        {
            sites.Add(site);
        }
        SaveSites(appRoot, sites);
    }

    public static void DeleteSite(string appRoot, string siteId)
    {
        var sites = GetAllSites(appRoot);
        sites.RemoveAll(s => s.Id == siteId);
        SaveSites(appRoot, sites);
    }

    public static string DetectDocumentRoot(string projectDir)
    {
        // 1. Check Laravel / Modern framework public/
        string publicDir = Path.Combine(projectDir, "public");
        if (Directory.Exists(publicDir)) return publicDir;

        // 2. Check legacy / custom htdocs/ or web/ or dist/
        string htdocsDir = Path.Combine(projectDir, "htdocs");
        if (Directory.Exists(htdocsDir)) return htdocsDir;

        string webDir = Path.Combine(projectDir, "web");
        if (Directory.Exists(webDir)) return webDir;

        string distDir = Path.Combine(projectDir, "dist");
        if (Directory.Exists(distDir)) return distDir;

        return projectDir;
    }

    /// <summary>
    /// Detects project framework and recommended configuration.
    /// </summary>
    public static (string Type, string DocRoot, int RecommendedProxyPort) DetectProjectType(string projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
            return ("php", projectDir, 3000);

        string packageJson = Path.Combine(projectDir, "package.json");
        string composerJson = Path.Combine(projectDir, "composer.json");

        // Check Node.js / SSR projects
        if (File.Exists(packageJson))
        {
            try
            {
                string content = File.ReadAllText(packageJson);
                // Next.js, Nuxt, Astro, SvelteKit, Nest, Express
                if (content.Contains("\"next\"") || content.Contains("\"nuxt\"") ||
                    content.Contains("\"astro\"") || content.Contains("\"@sveltejs/kit\"") ||
                    content.Contains("\"express\"") || content.Contains("\"@nestjs/core\""))
                {
                    return ("proxy", projectDir, 3000);
                }
            }
            catch { }
        }

        // Check Composer / PHP projects
        string docRoot = DetectDocumentRoot(projectDir);
        return ("php", docRoot, 3000);
    }

    public static string GetUpstreamName(string? version, string activePhp)
    {
        if (string.IsNullOrWhiteSpace(version) ||
            version.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            version.Equals(activePhp, StringComparison.OrdinalIgnoreCase))
        {
            return "php_default";
        }

        string sanitized = Regex.Replace(version, @"[^a-zA-Z0-9_]", "_");
        return $"php_{sanitized}";
    }

    public static void GenerateUpstreamsConfig(string appRoot, AppConfig config, string nginxConfDir)
    {
        string upstreamsOutputFile = Path.Combine(nginxConfDir, "upstreams.conf");
        var sb = new StringBuilder();
        sb.AppendLine("# Automatically generated by Dev Lite Server. Do not edit manually.");
        sb.AppendLine();

        int workerCount = config.EffectivePhpWorkerCount;

        // 1. Primary active PHP upstream pool
        sb.AppendLine($"# Upstream pool for default PHP ({config.ActivePhp})");
        sb.AppendLine("upstream php_default {");
        for (int i = 0; i < workerCount; i++)
        {
            sb.AppendLine($"    server 127.0.0.1:{config.PhpFastCgiPort + i};");
        }
        sb.AppendLine("    keepalive 16;");
        sb.AppendLine("}");
        sb.AppendLine();

        // 2. Extra PHP version upstreams used by configured sites
        var sites = GetAllSites(appRoot);
        var extraVersions = sites
            .Select(s => s.PhpVersion)
            .Where(v => !string.IsNullOrWhiteSpace(v) &&
                        !v.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                        !v.Equals(config.ActivePhp, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var version in extraVersions)
        {
            string customPhpExe = Path.Combine(appRoot, "bin", "php", version, "php-cgi.exe");
            if (!File.Exists(customPhpExe)) continue;

            string upstreamName = GetUpstreamName(version, config.ActivePhp);
            int basePort = Services.PhpService.GetPortForVersion(version, config.PhpFastCgiPort);

            sb.AppendLine($"# Upstream pool for {version}");
            sb.AppendLine($"upstream {upstreamName} {{");
            for (int i = 0; i < workerCount; i++)
            {
                sb.AppendLine($"    server 127.0.0.1:{basePort + i};");
            }
            sb.AppendLine("    keepalive 16;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        File.WriteAllText(upstreamsOutputFile, sb.ToString());
    }

    public static void GenerateVhostsConfig(string appRoot, AppConfig config, string nginxConfDir)
    {
        var sites = GetAllSites(appRoot);
        string vhostsOutputFile = Path.Combine(nginxConfDir, "vhosts.conf");

        var sb = new StringBuilder();
        sb.AppendLine("# Automatically generated by Dev Lite Server. Do not edit manually.");
        sb.AppendLine();

        foreach (var site in sites)
        {
            if (string.IsNullOrWhiteSpace(site.Domain) || string.IsNullOrWhiteSpace(site.PhysicalPath))
                continue;

            string docRoot = !string.IsNullOrEmpty(site.DocumentRoot) ? site.DocumentRoot : site.PhysicalPath;
            string forwardRoot = docRoot.Replace('\\', '/');

            string upstreamName = GetUpstreamName(site.PhpVersion, config.ActivePhp);

            if (site.IsProxy)
            {
                sb.AppendLine($"# Reverse Proxy Virtual Host for {site.Domain} -> http://127.0.0.1:{site.ProxyPort}");
            }
            else
            {
                sb.AppendLine($"# Virtual Host for {site.Domain} (PHP: {site.PhpVersion}, Upstream: {upstreamName})");
            }
            sb.AppendLine("server {");
            sb.AppendLine($"    listen       {config.HttpPort};");

            // SSL Configuration
            if (site.SslEnabled)
            {
                try
                {
                    var (certPath, keyPath) = SslCertificateManager.EnsureCertificate(appRoot, site.Domain);
                    sb.AppendLine($"    listen       {config.HttpsPort} ssl;");
                    sb.AppendLine($"    ssl_certificate      \"{certPath}\";");
                    sb.AppendLine($"    ssl_certificate_key  \"{keyPath}\";");
                    sb.AppendLine("    ssl_protocols        TLSv1.2 TLSv1.3;");
                    sb.AppendLine("    ssl_ciphers          HIGH:!aNULL:!MD5;");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    # SSL Generation Warning: {ex.Message}");
                }
            }

            sb.AppendLine($"    server_name  {site.Domain};");

            if (site.IsProxy)
            {
                // Reverse Proxy for Node.js / SSR / Vite with WebSocket upgrade support
                int targetPort = site.ProxyPort > 0 ? site.ProxyPort : 3000;
                sb.AppendLine();
                sb.AppendLine("    location / {");
                sb.AppendLine($"        proxy_pass         http://127.0.0.1:{targetPort};");
                sb.AppendLine("        proxy_http_version 1.1;");
                sb.AppendLine("        proxy_set_header   Upgrade $http_upgrade;");
                sb.AppendLine("        proxy_set_header   Connection \"upgrade\";");
                sb.AppendLine("        proxy_set_header   Host $host;");
                sb.AppendLine("        proxy_set_header   X-Real-IP $remote_addr;");
                sb.AppendLine("        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;");
                sb.AppendLine("        proxy_set_header   X-Forwarded-Proto $scheme;");
                sb.AppendLine("        proxy_cache_bypass $http_upgrade;");
                sb.AppendLine("        proxy_read_timeout 86400s;");
                sb.AppendLine("        proxy_send_timeout 86400s;");
                sb.AppendLine("    }");
            }
            else
            {
                // Standard PHP-CGI FastCGI Front Controller
                sb.AppendLine($"    root         \"{forwardRoot}\";");
                sb.AppendLine("    index        index.php index.html index.htm;");
                sb.AppendLine();
                sb.AppendLine("    location / {");
                sb.AppendLine("        try_files $uri $uri/ /index.php?$query_string;");
                sb.AppendLine("    }");
                sb.AppendLine();

                // Serve Laravel/PHP /storage/ assets safely (avoids Windows NTFS symlink Error 448 and PHP-only 404 trap)
                AppendStorageLocationBlock(sb, site, docRoot, upstreamName);

                sb.AppendLine();
                sb.AppendLine("    location ~ \\.php$ {");
                sb.AppendLine("        try_files $uri =404;");
                sb.AppendLine($"        fastcgi_pass   {upstreamName};");
                sb.AppendLine("        fastcgi_index  index.php;");
                sb.AppendLine("        fastcgi_param  SCRIPT_FILENAME $document_root$fastcgi_script_name;");
                sb.AppendLine("        include        fastcgi_params;");
                sb.AppendLine("        fastcgi_keep_conn on;");
                sb.AppendLine("    }");
            }

            sb.AppendLine();
            sb.AppendLine("    error_page   500 502 503 504  /50x.html;");
            sb.AppendLine("    location = /50x.html {");
            sb.AppendLine("        root   html;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        File.WriteAllText(vhostsOutputFile, sb.ToString());
    }

    private static void AppendStorageLocationBlock(StringBuilder sb, SiteItem site, string docRoot, string upstreamName)
    {
        string storageAppPublic = Path.Combine(site.PhysicalPath, "storage", "app", "public");
        string publicStorage = Path.Combine(docRoot, "storage");
        string? aliasTarget = ResolveStoragePublicTarget(site.PhysicalPath, docRoot, publicStorage, storageAppPublic);

        sb.AppendLine("    location ^~ /storage/ {");
        if (!string.IsNullOrEmpty(aliasTarget))
        {
            string aliasForward = Path.GetFullPath(aliasTarget).TrimEnd('\\', '/').Replace('\\', '/');
            sb.AppendLine($"        alias \"{aliasForward}/\";");
            sb.AppendLine("        error_page 403 404 = @storage_fallback;");
        }
        else
        {
            sb.AppendLine("        try_files $uri @storage_fallback;");
        }
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    location @storage_fallback {");
        sb.AppendLine("        include        fastcgi_params;");
        sb.AppendLine($"        fastcgi_pass   {upstreamName};");
        sb.AppendLine("        fastcgi_index  index.php;");
        sb.AppendLine("        fastcgi_param  SCRIPT_FILENAME $document_root/index.php;");
        sb.AppendLine("        fastcgi_param  SCRIPT_NAME /index.php;");
        sb.AppendLine("        fastcgi_keep_conn on;");
        sb.AppendLine("    }");
    }

    private static string? ResolveStoragePublicTarget(string physicalPath, string docRoot, string publicStorage, string storageAppPublic)
    {
        try
        {
            bool isReparse = IsReparsePoint(publicStorage);
            bool hasPublicStorageDir = Directory.Exists(publicStorage);

            if (hasPublicStorageDir && !isReparse)
            {
                // Real directory inside docRoot; serve directly unless empty while storage/app/public exists
                if (Directory.Exists(storageAppPublic) && !Directory.EnumerateFileSystemEntries(publicStorage).Any())
                {
                    return storageAppPublic;
                }
                return null;
            }

            if (Directory.Exists(storageAppPublic))
            {
                EnsureLaravelStorageJunction(physicalPath, docRoot, publicStorage, storageAppPublic, isReparse);
                return storageAppPublic;
            }

            if (hasPublicStorageDir && isReparse)
            {
                var resolved = Directory.ResolveLinkTarget(publicStorage, returnFinalTarget: true);
                if (resolved != null && Directory.Exists(resolved.FullName))
                {
                    return resolved.FullName;
                }
            }
        }
        catch
        {
            if (Directory.Exists(storageAppPublic))
            {
                return storageAppPublic;
            }
        }

        return null;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
            }
            if (Directory.Exists(path))
            {
                return (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0;
            }
        }
        catch
        {
            // Ignore attribute read failure
        }
        return false;
    }

    private static void EnsureLaravelStorageJunction(string physicalPath, string docRoot, string publicStorage, string storageAppPublic, bool isReparse)
    {
        try
        {
            if (!Directory.Exists(docRoot)) return;

            string expectedTarget = Path.GetFullPath(storageAppPublic).TrimEnd('\\', '/');
            string projectRoot = Path.GetFullPath(physicalPath).TrimEnd('\\', '/');

            if (isReparse)
            {
                var resolved = Directory.ResolveLinkTarget(publicStorage, returnFinalTarget: true);
                string? resolvedFull = resolved != null
                    ? Path.GetFullPath(resolved.FullName).TrimEnd('\\', '/')
                    : null;

                bool targetExists = resolvedFull != null && Directory.Exists(resolvedFull);
                if (targetExists && string.Equals(resolvedFull, expectedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                bool insideProject = resolvedFull != null &&
                    resolvedFull.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                if (targetExists && insideProject)
                {
                    return;
                }

                // Stale external (e.g. Herd/Laragon) or dangling junction/symlink: remove only the reparse point
                if (Directory.Exists(publicStorage))
                {
                    try
                    {
                        Directory.Delete(publicStorage, recursive: false);
                    }
                    catch
                    {
                        using var rmProc = Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rmdir \"{publicStorage}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        rmProc?.WaitForExit(2000);
                    }
                }
                else if (File.Exists(publicStorage))
                {
                    File.Delete(publicStorage);
                }
            }
            else if (!File.Exists(Path.Combine(physicalPath, "artisan")))
            {
                return;
            }

            if (!Directory.Exists(publicStorage) && !File.Exists(publicStorage))
            {
                // NTFS Directory Junction (mklink /J) works without Administrator/UAC privileges
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /J \"{publicStorage}\" \"{expectedTarget}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                proc?.WaitForExit(3000);
            }
        }
        catch
        {
            // Non-blocking: Nginx alias still serves storage/app/public directly
        }
    }

    public static bool NeedsHostsSync(string appRoot)
    {
        var sites = GetAllSites(appRoot);
        string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        if (!File.Exists(hostsPath)) return false;

        try
        {
            string currentHosts = File.ReadAllText(hostsPath);
            foreach (var site in sites)
            {
                if (!currentHosts.Contains(site.Domain, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static async Task<bool> SyncHostsFileBatchAsync(string appRoot)
    {
        var sites = GetAllSites(appRoot);
        string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        if (!File.Exists(hostsPath)) return false;

        string currentHosts;
        try
        {
            currentHosts = await File.ReadAllTextAsync(hostsPath);
        }
        catch
        {
            return false;
        }

        // Build desired vhosts block
        var blockSb = new StringBuilder();
        blockSb.AppendLine(HostsMarkerStart);
        foreach (var site in sites)
        {
            blockSb.AppendLine($"127.0.0.1 {site.Domain}");
        }
        blockSb.Append(HostsMarkerEnd);

        string newHosts;
        int startIndex = currentHosts.IndexOf(HostsMarkerStart, StringComparison.OrdinalIgnoreCase);
        int endIndex = currentHosts.IndexOf(HostsMarkerEnd, StringComparison.OrdinalIgnoreCase);

        if (startIndex >= 0 && endIndex >= 0 && endIndex > startIndex)
        {
            string before = currentHosts[..startIndex];
            string after = currentHosts[(endIndex + HostsMarkerEnd.Length)..];
            newHosts = before + blockSb.ToString() + after;
        }
        else
        {
            newHosts = currentHosts.TrimEnd() + Environment.NewLine + Environment.NewLine + blockSb.ToString() + Environment.NewLine;
        }

        // Write via elevated PowerShell script
        string tempContentFile = Path.Combine(Path.GetTempPath(), $"dls_hosts_{Guid.NewGuid():N}.txt");
        string tempScript = Path.Combine(Path.GetTempPath(), $"dls_sync_{Guid.NewGuid():N}.ps1");

        try
        {
            await File.WriteAllTextAsync(tempContentFile, newHosts, Encoding.UTF8);

            string psScript = $@"
$ErrorActionPreference = 'Stop'
[System.IO.File]::WriteAllText('{hostsPath}', [System.IO.File]::ReadAllText('{tempContentFile}', [System.Text.Encoding]::UTF8), [System.Text.Encoding]::UTF8)
exit 0
";
            await File.WriteAllTextAsync(tempScript, psScript, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScript}\"",
                UseShellExecute = true,
                Verb = "runas", // UAC elevation prompt
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { if (File.Exists(tempScript)) File.Delete(tempScript); } catch { }
            try { if (File.Exists(tempContentFile)) File.Delete(tempContentFile); } catch { }
        }
    }
}
