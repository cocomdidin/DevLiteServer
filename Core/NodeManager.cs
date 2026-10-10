using System.Diagnostics;

namespace DevLiteServer.Core;

public static class NodeManager
{
    public static (bool isFound, string path, string version) DetectSystemNode()
    {
        try
        {
            string[] candidateDirs =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs")
            ];

            foreach (var dir in candidateDirs)
            {
                string exePath = Path.Combine(dir, "node.exe");
                if (File.Exists(exePath))
                {
                    string ver = GetNodeVersion(exePath);
                    return (true, dir, ver);
                }
            }

            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var part in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    // Ignore our own internal paths
                    if (part.Contains("LiteServer", StringComparison.OrdinalIgnoreCase)) continue;

                    string exe = Path.Combine(part, "node.exe");
                    if (File.Exists(exe))
                    {
                        string ver = GetNodeVersion(exe);
                        return (true, part, ver);
                    }
                }
            }
        }
        catch { }

        return (false, "", "");
    }

    private static string GetNodeVersion(string nodeExePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(nodeExePath);
            if (!string.IsNullOrEmpty(info.ProductVersion))
            {
                string v = info.ProductVersion.Trim();
                return v.StartsWith('v') ? v : $"v{v}";
            }
        }
        catch { }
        return "Unknown";
    }

    public static List<string> GetInstalledVersions(string appRoot)
    {
        var list = new List<string>();
        string nodeRoot = Path.Combine(appRoot, "bin", "nodejs");
        if (!Directory.Exists(nodeRoot)) return list;

        foreach (var dir in Directory.GetDirectories(nodeRoot))
        {
            string name = Path.GetFileName(dir);
            if (string.Equals(name, "current", StringComparison.OrdinalIgnoreCase)) continue;

            if (File.Exists(Path.Combine(dir, "node.exe")))
            {
                list.Add(name);
            }
        }
        return list;
    }

    public static bool UpdateCurrentJunction(string appRoot, string activeVersion)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(activeVersion) || string.Equals(activeVersion, "current", StringComparison.OrdinalIgnoreCase))
                return false;

            string nodeRoot = Path.Combine(appRoot, "bin", "nodejs");
            if (!Directory.Exists(nodeRoot)) Directory.CreateDirectory(nodeRoot);

            string targetDir;
            if (string.Equals(activeVersion, "system", StringComparison.OrdinalIgnoreCase))
            {
                var (isFound, sysPath, _) = DetectSystemNode();
                if (!isFound || !Directory.Exists(sysPath)) return false;
                targetDir = sysPath;
            }
            else
            {
                targetDir = Path.Combine(nodeRoot, activeVersion);
                if (!Directory.Exists(targetDir)) return false;
            }

            string junctionPath = Path.Combine(nodeRoot, "current");

            if (Path.Exists(junctionPath))
            {
                var dirInfo = new DirectoryInfo(junctionPath);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    try
                    {
                        string? currentTarget = dirInfo.LinkTarget;
                        if (!string.IsNullOrEmpty(currentTarget) &&
                            string.Equals(Path.GetFullPath(currentTarget), Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        dirInfo.Delete();
                    }
                    catch
                    {
                        var psiRm = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rmdir \"{junctionPath}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using var procRm = Process.Start(psiRm);
                        procRm?.WaitForExit(2000);
                    }
                }
                else
                {
                    return false;
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetDir}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);

            return Directory.Exists(junctionPath);
        }
        catch
        {
            return false;
        }
    }
}
