namespace DevLiteServer.Core;

public class PhpExtensionInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; }
    public bool IsZend { get; set; }
    public string DllFileName { get; set; } = "";
}

public static class PhpExtensionManager
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["curl"] = "Client URL Library for HTTP requests, REST APIs, and file downloads",
        ["fileinfo"] = "Fast MIME-type detection and binary file inspections",
        ["gd"] = "Image manipulation library (PNG, JPEG, WebP, resizing, watermarks)",
        ["intl"] = "Internationalization functions, Unicode collation, and currency formatting",
        ["mbstring"] = "Multibyte string support (UTF-8, emoji, character encodings)",
        ["mysqli"] = "MySQL Improved Extension for procedural and object-oriented MySQL access",
        ["openssl"] = "OpenSSL cryptography, HTTPS transport, and asymmetric key generation",
        ["pdo_mysql"] = "PDO driver for MySQL database engines (standard for Laravel & modern PHP)",
        ["pdo_pgsql"] = "PDO driver for PostgreSQL database engines",
        ["pdo_sqlite"] = "PDO driver for SQLite 3 database engines",
        ["pdo_odbc"] = "PDO driver for ODBC database connections",
        ["pdo_firebird"] = "PDO driver for Firebird database engines",
        ["pgsql"] = "Native driver for PostgreSQL database servers",
        ["soap"] = "SOAP client and server protocol handler for XML web services",
        ["sockets"] = "Low-level BSD socket communication interface",
        ["sodium"] = "Modern libsodium cryptographic library for password hashing & encryption",
        ["sqlite3"] = "Native SQLite 3 embeddable database interface",
        ["zip"] = "ZIP archive creation, extraction, and file compression",
        ["opcache"] = "Zend OPcache bytecode caching engine for fast execution",
        ["xdebug"] = "Step debugging, profiler, and code coverage tool for development",
        ["sqlsrv"] = "Microsoft SQL Server driver for PHP on Windows",
        ["pdo_sqlsrv"] = "PDO driver for Microsoft SQL Server connections",
        ["redis"] = "High-performance in-memory Redis client extension",
        ["exif"] = "EXIF image metadata reader (captures camera & GPS data)",
        ["bz2"] = "Bzip2 high-ratio compression and decompression library",
        ["ldap"] = "Lightweight Directory Access Protocol (Active Directory / LDAP)",
        ["tidy"] = "HTML Tidy syntax cleaner and DOM tree fixer",
        ["xsl"] = "XSLT transformations for XML documents",
        ["gmp"] = "GNU Multiple Precision arithmetic for big numbers",
        ["shmop"] = "Shared memory operations for inter-process communication",
        ["ftp"] = "FTP client interface for remote file transfers"
    };

    public static List<PhpExtensionInfo> GetExtensions(string phpDir)
    {
        var list = new List<PhpExtensionInfo>();
        string extDir = Path.Combine(phpDir, "ext");
        string phpIni = Path.Combine(phpDir, "php.ini");

        var enabledSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var zendSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(phpIni))
        {
            var lines = File.ReadAllLines(phpIni);
            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith(';') || line.StartsWith('#')) continue;

                if (line.StartsWith("extension=", StringComparison.OrdinalIgnoreCase))
                {
                    string extVal = line["extension=".Length..].Trim().Trim('"');
                    string clean = CleanExtensionName(extVal);
                    enabledSet.Add(clean);
                }
                else if (line.StartsWith("zend_extension=", StringComparison.OrdinalIgnoreCase))
                {
                    string extVal = line["zend_extension=".Length..].Trim().Trim('"');
                    string clean = CleanExtensionName(extVal);
                    zendSet.Add(clean);
                    enabledSet.Add(clean);
                }
            }
        }

        // 1. Scan ext folder for physical DLLs
        if (Directory.Exists(extDir))
        {
            var dllFiles = Directory.GetFiles(extDir, "*.dll");
            foreach (var dll in dllFiles)
            {
                string fileName = Path.GetFileName(dll);
                string cleanName = CleanExtensionName(fileName);
                bool isZend = cleanName.Equals("opcache", StringComparison.OrdinalIgnoreCase) ||
                              cleanName.Equals("xdebug", StringComparison.OrdinalIgnoreCase);

                Descriptions.TryGetValue(cleanName, out var desc);

                list.Add(new PhpExtensionInfo
                {
                    Name = cleanName,
                    DisplayName = cleanName,
                    Description = desc ?? $"PHP extension module ({fileName})",
                    IsEnabled = enabledSet.Contains(cleanName),
                    IsZend = isZend,
                    DllFileName = fileName
                });
            }
        }

        return list.OrderByDescending(e => e.IsEnabled).ThenBy(e => e.Name).ToList();
    }

    public static bool SetExtensionEnabled(string phpDir, string extName, bool enable)
    {
        string phpIni = Path.Combine(phpDir, "php.ini");
        if (!File.Exists(phpIni)) return false;

        try
        {
            var lines = File.ReadAllLines(phpIni).ToList();
            bool isZend = extName.Equals("opcache", StringComparison.OrdinalIgnoreCase) ||
                          extName.Equals("xdebug", StringComparison.OrdinalIgnoreCase);

            string directive = isZend ? "zend_extension" : "extension";
            bool found = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                string clean = line.TrimStart(';', '#', ' ').Trim();

                if (clean.StartsWith($"{directive}=", StringComparison.OrdinalIgnoreCase) ||
                    clean.StartsWith("extension=", StringComparison.OrdinalIgnoreCase))
                {
                    int eqIndex = clean.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        string val = clean[(eqIndex + 1)..].Trim().Trim('"');
                        if (CleanExtensionName(val).Equals(extName, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            lines[i] = enable ? $"{directive}={extName}" : $";{directive}={extName}";
                            break;
                        }
                    }
                }
            }

            if (!found && enable)
            {
                // Append under Extensions section or at end of file
                int targetIdx = lines.FindIndex(l => l.Contains("; Dynamic Extensions", StringComparison.OrdinalIgnoreCase) ||
                                                     l.Contains("[ExtensionList]", StringComparison.OrdinalIgnoreCase) ||
                                                     l.Contains("extension=", StringComparison.OrdinalIgnoreCase));
                if (targetIdx >= 0)
                {
                    lines.Insert(targetIdx + 1, $"{directive}={extName}");
                }
                else
                {
                    lines.Add($"{directive}={extName}");
                }
            }

            File.WriteAllLines(phpIni, lines);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CleanExtensionName(string raw)
    {
        string name = Path.GetFileNameWithoutExtension(raw);
        if (name.StartsWith("php_", StringComparison.OrdinalIgnoreCase))
        {
            name = name[4..];
        }
        return name.Trim();
    }
}
