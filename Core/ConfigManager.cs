namespace LiteServer.Core;

public class AppConfig
{
    public bool AutoStartServices { get; set; } = true;
    public bool CheckUpdatesOnStart { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;

    // Enabled services
    public bool EnableNginx { get; set; } = true;
    public bool EnablePhp { get; set; } = true;
    public bool EnableMysql { get; set; } = true;
    public bool EnablePostgresql { get; set; } = false;
    public bool EnableRedis { get; set; } = false;
    public bool EnableMailpit { get; set; } = true;

    // Active versions
    public string ActivePhp { get; set; } = "php-8.4";
    public string ActiveNode { get; set; } = "node-v24";

    // Ports
    public int HttpPort { get; set; } = 80;
    public int PhpFastCgiPort { get; set; } = 9000;
    public int MysqlPort { get; set; } = 3306;
    public int PostgreSqlPort { get; set; } = 5432;
    public int RedisPort { get; set; } = 6379;
    public int MailpitSmtpPort { get; set; } = 1025;
    public int MailpitWebPort { get; set; } = 8025;
}

public static class ConfigManager
{
    public static AppConfig Load(string iniPath)
    {
        var config = new AppConfig();
        if (!File.Exists(iniPath))
        {
            Save(iniPath, config);
            return config;
        }

        string currentSection = "";
        foreach (var rawLine in File.ReadAllLines(iniPath))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            int eqIndex = line.IndexOf('=');
            if (eqIndex <= 0) continue;

            string key = line[..eqIndex].Trim();
            string val = line[(eqIndex + 1)..].Trim();

            switch (currentSection.ToUpperInvariant())
            {
                case "GENERAL":
                    if (key.Equals("AutoStartServices", StringComparison.OrdinalIgnoreCase))
                        config.AutoStartServices = bool.Parse(val);
                    else if (key.Equals("CheckUpdatesOnStart", StringComparison.OrdinalIgnoreCase))
                        config.CheckUpdatesOnStart = bool.Parse(val);
                    else if (key.Equals("MinimizeToTray", StringComparison.OrdinalIgnoreCase))
                        config.MinimizeToTray = bool.Parse(val);
                    else if (key.Equals("StartWithWindows", StringComparison.OrdinalIgnoreCase))
                        config.StartWithWindows = bool.Parse(val);
                    break;

                case "SERVICES":
                    if (key.Equals("Nginx", StringComparison.OrdinalIgnoreCase)) config.EnableNginx = bool.Parse(val);
                    else if (key.Equals("Php", StringComparison.OrdinalIgnoreCase)) config.EnablePhp = bool.Parse(val);
                    else if (key.Equals("Mysql", StringComparison.OrdinalIgnoreCase)) config.EnableMysql = bool.Parse(val);
                    else if (key.Equals("Postgresql", StringComparison.OrdinalIgnoreCase)) config.EnablePostgresql = bool.Parse(val);
                    else if (key.Equals("Redis", StringComparison.OrdinalIgnoreCase)) config.EnableRedis = bool.Parse(val);
                    else if (key.Equals("Mailpit", StringComparison.OrdinalIgnoreCase)) config.EnableMailpit = bool.Parse(val);
                    break;

                case "VERSIONS":
                    if (key.Equals("ActivePhp", StringComparison.OrdinalIgnoreCase)) config.ActivePhp = val;
                    else if (key.Equals("ActiveNode", StringComparison.OrdinalIgnoreCase)) config.ActiveNode = val;
                    break;

                case "PORTS":
                    if (key.Equals("HttpPort", StringComparison.OrdinalIgnoreCase)) config.HttpPort = int.Parse(val);
                    else if (key.Equals("PhpFastCgiPort", StringComparison.OrdinalIgnoreCase)) config.PhpFastCgiPort = int.Parse(val);
                    else if (key.Equals("MysqlPort", StringComparison.OrdinalIgnoreCase)) config.MysqlPort = int.Parse(val);
                    else if (key.Equals("PostgreSqlPort", StringComparison.OrdinalIgnoreCase)) config.PostgreSqlPort = int.Parse(val);
                    else if (key.Equals("RedisPort", StringComparison.OrdinalIgnoreCase)) config.RedisPort = int.Parse(val);
                    else if (key.Equals("MailpitSmtpPort", StringComparison.OrdinalIgnoreCase)) config.MailpitSmtpPort = int.Parse(val);
                    else if (key.Equals("MailpitWebPort", StringComparison.OrdinalIgnoreCase)) config.MailpitWebPort = int.Parse(val);
                    break;
            }
        }

        return config;
    }

    public static void Save(string iniPath, AppConfig config)
    {
        var lines = new List<string>
        {
            "[General]",
            $"AppName=Local Lite Server",
            $"StartWithWindows={config.StartWithWindows.ToString().ToLower()}",
            $"MinimizeToTray={config.MinimizeToTray.ToString().ToLower()}",
            $"AutoStartServices={config.AutoStartServices.ToString().ToLower()}",
            $"CheckUpdatesOnStart={config.CheckUpdatesOnStart.ToString().ToLower()}",
            "",
            "[Services]",
            $"Nginx={config.EnableNginx.ToString().ToLower()}",
            $"Php={config.EnablePhp.ToString().ToLower()}",
            $"Mysql={config.EnableMysql.ToString().ToLower()}",
            $"Postgresql={config.EnablePostgresql.ToString().ToLower()}",
            $"Redis={config.EnableRedis.ToString().ToLower()}",
            $"Mailpit={config.EnableMailpit.ToString().ToLower()}",
            "",
            "[Versions]",
            $"ActivePhp={config.ActivePhp}",
            $"ActiveNode={config.ActiveNode}",
            "",
            "[Ports]",
            $"HttpPort={config.HttpPort}",
            $"PhpFastCgiPort={config.PhpFastCgiPort}",
            $"MysqlPort={config.MysqlPort}",
            $"PostgreSqlPort={config.PostgreSqlPort}",
            $"RedisPort={config.RedisPort}",
            $"MailpitSmtpPort={config.MailpitSmtpPort}",
            $"MailpitWebPort={config.MailpitWebPort}"
        };

        File.WriteAllLines(iniPath, lines);
    }
}
