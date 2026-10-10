using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public bool HasPublicSubfolder =>
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
                    string docRoot = DetectDocumentRoot(normalizedDir);

                    var newSite = new SiteItem
                    {
                        Name = folderName,
                        Domain = domain,
                        PhysicalPath = normalizedDir,
                        DocumentRoot = docRoot,
                        PhpVersion = "default",
                        SslEnabled = false,
                        IsExternal = false
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
        string publicDir = Path.Combine(projectDir, "public");
        string htdocsDir = Path.Combine(projectDir, "htdocs");

        if (Directory.Exists(publicDir)) return publicDir;
        if (Directory.Exists(htdocsDir)) return htdocsDir;
        return projectDir;
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

            // Determine FastCGI port for this site
            int phpPort = config.PhpFastCgiPort;
            if (!string.IsNullOrEmpty(site.PhpVersion) &&
                !site.PhpVersion.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                !site.PhpVersion.Equals(config.ActivePhp, StringComparison.OrdinalIgnoreCase))
            {
                string customPhpExe = Path.Combine(appRoot, "bin", "php", site.PhpVersion, "php-cgi.exe");
                if (File.Exists(customPhpExe))
                {
                    phpPort = Services.PhpService.GetPortForVersion(site.PhpVersion, config.PhpFastCgiPort);
                }
            }

            sb.AppendLine($"# Virtual Host for {site.Domain} (PHP: {site.PhpVersion}, Port: {phpPort})");
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
            sb.AppendLine($"    root         \"{forwardRoot}\";");
            sb.AppendLine("    index        index.php index.html index.htm;");
            sb.AppendLine();
            sb.AppendLine("    location / {");
            sb.AppendLine("        try_files $uri $uri/ /index.php?$query_string;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    location ~ \\.php$ {");
            sb.AppendLine("        try_files $uri =404;");
            sb.AppendLine($"        fastcgi_pass   127.0.0.1:{phpPort};");
            sb.AppendLine("        fastcgi_index  index.php;");
            sb.AppendLine("        fastcgi_param  SCRIPT_FILENAME $document_root$fastcgi_script_name;");
            sb.AppendLine("        include        fastcgi_params;");
            sb.AppendLine("    }");
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
