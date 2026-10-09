namespace DevLiteServer.Core.Downloader;

public class PackageItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = ""; // "PHP", "Node", or "Service"
    public string ServiceKey { get; set; } = ""; // "mysql", "postgresql", "redis", "mailpit"
    public string Tag { get; set; } = "";      // "Current", "Active LTS", etc.
    public string DownloadUrl { get; set; } = "";
    public string FolderName { get; set; } = ""; // folder inside bin/php, bin/nodejs, or service folder
    public long ApproximateSizeBytes { get; set; }

    public string GetInstallDirectory(string appRoot)
    {
        if (Category.Equals("PHP", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(appRoot, "bin", "php", FolderName);

        if (Category.Equals("Node", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(appRoot, "bin", "nodejs", FolderName);

        // Service packages (mysql, postgresql, redis, mailpit)
        return Path.Combine(appRoot, "bin", FolderName);
    }

    public bool IsInstalled(string appRoot)
    {
        string dir = GetInstallDirectory(appRoot);
        if (!Directory.Exists(dir)) return false;

        if (Category.Equals("PHP", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(Path.Combine(dir, "php-cgi.exe")) || File.Exists(Path.Combine(dir, "php.exe"));
        }
        else if (Category.Equals("Node", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(Path.Combine(dir, "node.exe"));
        }
        else if (Category.Equals("Service", StringComparison.OrdinalIgnoreCase))
        {
            string key = !string.IsNullOrEmpty(ServiceKey) ? ServiceKey : FolderName;
            if (key.Equals("mysql", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(Path.Combine(dir, "bin", "mysqld.exe"))) return true;
                return Directory.GetFiles(dir, "mysqld.exe", SearchOption.AllDirectories).Length > 0;
            }
            if (key.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(Path.Combine(dir, "bin", "postgres.exe"))) return true;
                return Directory.GetFiles(dir, "postgres.exe", SearchOption.AllDirectories).Length > 0;
            }
            if (key.Equals("redis", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(Path.Combine(dir, "redis-server.exe"))) return true;
                return Directory.GetFiles(dir, "redis-server.exe", SearchOption.AllDirectories).Length > 0;
            }
            if (key.Equals("mailpit", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(Path.Combine(dir, "mailpit.exe"))) return true;
                return Directory.GetFiles(dir, "mailpit.exe", SearchOption.AllDirectories).Length > 0;
            }
        }

        return false;
    }

    public bool IsActive(AppConfig config)
    {
        if (Category.Equals("PHP", StringComparison.OrdinalIgnoreCase))
        {
            return FolderName.Equals(config.ActivePhp, StringComparison.OrdinalIgnoreCase) ||
                   Id.Equals(config.ActivePhp, StringComparison.OrdinalIgnoreCase);
        }
        else if (Category.Equals("Node", StringComparison.OrdinalIgnoreCase))
        {
            return FolderName.Equals(config.ActiveNode, StringComparison.OrdinalIgnoreCase) ||
                   Id.Equals(config.ActiveNode, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
