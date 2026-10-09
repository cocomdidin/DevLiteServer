namespace LiteServer.Core;

public static class AppPaths
{
    private static string? _cachedRoot;

    /// <summary>
    /// Resolves the true application root directory across both development
    /// (e.g. dotnet run from build/bin/Debug/net10.0-windows) and production
    /// (standalone publish in root folder).
    /// </summary>
    public static string ResolveRoot()
    {
        if (_cachedRoot != null) return _cachedRoot;

        string startDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(startDir))
        {
            startDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        string current = startDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 1. In development, walk up looking for project file
        string? probe = current;
        while (!string.IsNullOrEmpty(probe))
        {
            if (File.Exists(Path.Combine(probe, "LocalLiteServer.csproj")))
            {
                _cachedRoot = probe;
                return _cachedRoot;
            }
            probe = Path.GetDirectoryName(probe);
        }

        // 2. Check Environment.CurrentDirectory
        string cwd = Environment.CurrentDirectory;
        if (File.Exists(Path.Combine(cwd, "LocalLiteServer.csproj")))
        {
            _cachedRoot = cwd;
            return _cachedRoot;
        }

        // 3. In published / portable mode, look for bin/nginx or bin/php, skipping build/bin/obj
        probe = current;
        while (!string.IsNullOrEmpty(probe))
        {
            string folderName = Path.GetFileName(probe);
            if (!folderName.Equals("build", StringComparison.OrdinalIgnoreCase) &&
                !folderName.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                !folderName.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(Path.Combine(probe, "bin", "nginx")) ||
                    Directory.Exists(Path.Combine(probe, "bin", "php")) ||
                    File.Exists(Path.Combine(probe, "config.ini")))
                {
                    _cachedRoot = probe;
                    return _cachedRoot;
                }
            }
            probe = Path.GetDirectoryName(probe);
        }

        _cachedRoot = current;
        return _cachedRoot;
    }
}
