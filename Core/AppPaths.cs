namespace DevLiteServer.Core;

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
            if (File.Exists(Path.Combine(probe, "DevLiteServer.csproj")))
            {
                _cachedRoot = probe;
                return _cachedRoot;
            }
            probe = Path.GetDirectoryName(probe);
        }

        // 2. Check Environment.CurrentDirectory
        string cwd = Environment.CurrentDirectory;
        if (File.Exists(Path.Combine(cwd, "DevLiteServer.csproj")))
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

    /// <summary>
    /// Cleans up legacy multi-file .NET compile artifacts and satellite resource folders
    /// left in the application root from older non-single-file releases.
    /// Safely skipped in development environments.
    /// </summary>
    public static void CleanupLegacyArtifacts()
    {
        try
        {
            string root = ResolveRoot();

            // Safety guard: never delete anything in development mode
            if (File.Exists(Path.Combine(root, "DevLiteServer.csproj")))
            {
                return;
            }

            // 1. Delete legacy loose .dll, .pdb, and runtime json files in root directory
            string[] looseFiles = Directory.GetFiles(root, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in looseFiles)
            {
                string ext = Path.GetExtension(file);
                string fileName = Path.GetFileName(file);

                if (ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".pdb", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("DevLiteServer.deps.json", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("DevLiteServer.runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(file); } catch { }
                }
            }

            // 2. Delete legacy .NET runtime subfolders (runtimes and satellite localization)
            string[] legacyDirs = [
                "runtimes", "cs", "de", "es", "fr", "it", "ja",
                "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant"
            ];

            foreach (var dirName in legacyDirs)
            {
                string targetDir = Path.Combine(root, dirName);
                if (Directory.Exists(targetDir))
                {
                    try { Directory.Delete(targetDir, recursive: true); } catch { }
                }
            }
        }
        catch
        {
            // ponytail: Best effort cleanup; never block application startup
        }
    }
}
