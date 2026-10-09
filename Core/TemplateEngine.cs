namespace DevLiteServer.Core;

public static class TemplateEngine
{
    /// <summary>
    /// Processes a .tpl template file and writes the final configuration file.
    /// </summary>
    public static void ProcessTemplate(string templatePath, string outputPath, IDictionary<string, string> variables)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
        }

        string content = File.ReadAllText(templatePath);

        foreach (var (key, value) in variables)
        {
            content = content.Replace($"{{{{{key}}}}}", value);
        }

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, content);
    }

    /// <summary>
    /// Builds standard path variables based on application root directory.
    /// </summary>
    public static Dictionary<string, string> CreateStandardVariables(string appRoot, int httpPort = 80, int phpPort = 9000, int mysqlPort = 3306)
    {
        string normalizedRoot = appRoot.TrimEnd('\\', '/');
        string forwardSlashRoot = normalizedRoot.Replace('\\', '/');

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ROOT_DIR"] = normalizedRoot,
            ["ROOT_DIR_FORWARD"] = forwardSlashRoot,
            ["WWW_DIR"] = Path.Combine(normalizedRoot, "www"),
            ["WWW_DIR_FORWARD"] = $"{forwardSlashRoot}/www",
            ["HTTP_PORT"] = httpPort.ToString(),
            ["PHP_PORT"] = phpPort.ToString(),
            ["MYSQL_PORT"] = mysqlPort.ToString()
        };
    }
}
