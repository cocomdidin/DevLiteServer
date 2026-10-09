using System.Text.Json;

namespace DevLiteServer.Core.Downloader;

public static class PackageCatalog
{
    private static readonly List<PackageItem> DefaultPhpPackages =
    [
        new PackageItem
        {
            Id = "php-8.4",
            Name = "PHP 8.4",
            Category = "PHP",
            Tag = "Current Release (NTS x64)",
            DownloadUrl = "https://windows.php.net/downloads/releases/php-8.4.4-nts-Win32-vs17-x64.zip",
            FolderName = "php-8.4",
            ApproximateSizeBytes = 32_500_000
        },
        new PackageItem
        {
            Id = "php-8.3",
            Name = "PHP 8.3",
            Category = "PHP",
            Tag = "Active Support (NTS x64)",
            DownloadUrl = "https://windows.php.net/downloads/releases/php-8.3.17-nts-Win32-vs16-x64.zip",
            FolderName = "php-8.3",
            ApproximateSizeBytes = 31_800_000
        },
        new PackageItem
        {
            Id = "php-8.2",
            Name = "PHP 8.2",
            Category = "PHP",
            Tag = "Security Fixes (NTS x64)",
            DownloadUrl = "https://windows.php.net/downloads/releases/php-8.2.28-nts-Win32-vs16-x64.zip",
            FolderName = "php-8.2",
            ApproximateSizeBytes = 31_200_000
        },
        new PackageItem
        {
            Id = "php-8.1",
            Name = "PHP 8.1",
            Category = "PHP",
            Tag = "Security Fixes (NTS x64)",
            DownloadUrl = "https://windows.php.net/downloads/releases/archives/php-8.1.31-nts-Win32-vs16-x64.zip",
            FolderName = "php-8.1",
            ApproximateSizeBytes = 30_500_000
        },
        new PackageItem
        {
            Id = "php-7.4",
            Name = "PHP 7.4",
            Category = "PHP",
            Tag = "Legacy EOL (NTS x64)",
            DownloadUrl = "https://windows.php.net/downloads/releases/archives/php-7.4.33-nts-Win32-vc15-x64.zip",
            FolderName = "php-7.4",
            ApproximateSizeBytes = 25_800_000
        }
    ];

    private static readonly List<PackageItem> DefaultNodePackages =
    [
        new PackageItem
        {
            Id = "node-v24",
            Name = "Node.js v24",
            Category = "Node",
            Tag = "Current Release (v24.x x64)",
            DownloadUrl = "https://nodejs.org/dist/v24.2.0/node-v24.2.0-win-x64.zip",
            FolderName = "node-v24",
            ApproximateSizeBytes = 36_000_000
        },
        new PackageItem
        {
            Id = "node-v22",
            Name = "Node.js v22 (LTS)",
            Category = "Node",
            Tag = "Active LTS (Jod x64)",
            DownloadUrl = "https://nodejs.org/dist/v22.14.0/node-v22.14.0-win-x64.zip",
            FolderName = "node-v22",
            ApproximateSizeBytes = 35_200_000
        },
        new PackageItem
        {
            Id = "node-v20",
            Name = "Node.js v20 (LTS)",
            Category = "Node",
            Tag = "Maintenance LTS (Iron x64)",
            DownloadUrl = "https://nodejs.org/dist/v20.18.3/node-v20.18.3-win-x64.zip",
            FolderName = "node-v20",
            ApproximateSizeBytes = 32_800_000
        },
        new PackageItem
        {
            Id = "node-v18",
            Name = "Node.js v18",
            Category = "Node",
            Tag = "Maintenance (v18.x x64)",
            DownloadUrl = "https://nodejs.org/dist/v18.20.7/node-v18.20.7-win-x64.zip",
            FolderName = "node-v18",
            ApproximateSizeBytes = 28_400_000
        }
    ];

    public static List<PackageItem> GetPhpPackages() => [.. DefaultPhpPackages];

    public static List<PackageItem> GetNodePackages() => [.. DefaultNodePackages];

    private static readonly Dictionary<string, PackageItem> ServicePackages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postgresql"] = new PackageItem
        {
            Id = "service-postgresql",
            Name = "PostgreSQL 17",
            Category = "Service",
            ServiceKey = "postgresql",
            Tag = "PostgreSQL 17.2 Portable (x64)",
            DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-17.2-1-windows-x64-binaries.zip",
            FolderName = "postgresql",
            ApproximateSizeBytes = 105_000_000
        },
        ["redis"] = new PackageItem
        {
            Id = "service-redis",
            Name = "Redis 5.0",
            Category = "Service",
            ServiceKey = "redis",
            Tag = "Redis 5.0.14 Windows Portable (x64)",
            DownloadUrl = "https://github.com/tporadowski/redis/releases/download/v5.0.14.1/Redis-x64-5.0.14.1.zip",
            FolderName = "redis",
            ApproximateSizeBytes = 5_500_000
        },
        ["mailpit"] = new PackageItem
        {
            Id = "service-mailpit",
            Name = "Mailpit",
            Category = "Service",
            ServiceKey = "mailpit",
            Tag = "Mailpit SMTP & Web UI (x64)",
            DownloadUrl = "https://github.com/axllent/mailpit/releases/latest/download/mailpit-windows-amd64.zip",
            FolderName = "mailpit",
            ApproximateSizeBytes = 8_500_000
        },
        ["mysql"] = new PackageItem
        {
            Id = "service-mysql",
            Name = "MySQL 8.4 (LTS)",
            Category = "Service",
            ServiceKey = "mysql",
            Tag = "MySQL Community Server 8.4 LTS (x64)",
            DownloadUrl = "https://cdn.mysql.com/archives/mysql-8.4/mysql-8.4.4-winx64.zip",
            FolderName = "mysql",
            ApproximateSizeBytes = 230_000_000
        }
    };

    public static PackageItem? GetServicePackage(string serviceKey)
    {
        return ServicePackages.TryGetValue(serviceKey, out var pkg) ? pkg : null;
    }

    /// <summary>
    /// Attempts to query official windows.php.net releases.json to refresh
    /// latest patch versions dynamically if internet connectivity is available.
    /// </summary>
    public static async Task RefreshPhpReleasesAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.Add("User-Agent", "DevLiteServer/1.0");

            string json = await http.GetStringAsync("https://windows.php.net/downloads/releases/releases.json");
            using var doc = JsonDocument.Parse(json);

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                string majorMinor = property.Name; // e.g. "8.4", "8.3"
                var match = DefaultPhpPackages.FirstOrDefault(p => p.Id == $"php-{majorMinor}");
                if (match == null) continue;

                // Look for nts x64 build
                foreach (var buildProp in property.Value.EnumerateObject())
                {
                    string buildName = buildProp.Name.ToLowerInvariant();
                    if (buildName.Contains("nts") && buildName.Contains("x64") && buildProp.Value.TryGetProperty("zip", out var zipObj))
                    {
                        if (zipObj.TryGetProperty("path", out var pathProp))
                        {
                            string zipFile = pathProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(zipFile))
                            {
                                match.DownloadUrl = $"https://windows.php.net/downloads/releases/{zipFile}";
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently retain built-in verified default URLs
        }
    }
}
