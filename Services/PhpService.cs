using System.Diagnostics;
using System.Text.RegularExpressions;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class PhpWorkerInfo
{
    public string Version { get; set; } = "";
    public int BasePort { get; set; }
    public List<Process?> Processes { get; } = new();
    public CancellationTokenSource Cts { get; set; } = new();
}

public class PhpService : BaseService
{
    private readonly AppConfig _config;
    private CancellationTokenSource? _supervisorCts;
    private readonly Dictionary<string, PhpWorkerInfo> _extraWorkers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Process?> _primaryWorkers = new();

    public int WorkerCount => Math.Clamp(_config.PhpWorkerCount, 1, 16);

    public override string Name => "PHP (FastCGI)";
    public override int Port => _config.PhpFastCgiPort;
    public override bool IsInstalled => File.Exists(GetPhpCgiPath());

    public override void UpdatePort(int newPort)
    {
        _config.PhpFastCgiPort = newPort;
    }

    public PhpService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
        CheckInstallation();
    }

    public static int GetPortForVersion(string version, int defaultPort = 9000)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return defaultPort;
        }

        var match = Regex.Match(version, @"(\d+)\.(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int major) && int.TryParse(match.Groups[2].Value, out int minor))
        {
            return 9000 + (major * 100) + (minor * 10); // e.g. 8.4 -> 9840, 8.2 -> 9820, 7.4 -> 9740 (10 port slots per version)
        }

        return 9100 + Math.Abs(version.GetHashCode() % 50) * 10;
    }

    public static List<string> GetInstalledVersions(string appRoot)
    {
        var list = new List<string>();
        string phpRoot = Path.Combine(appRoot, "bin", "php");
        if (!Directory.Exists(phpRoot)) return list;

        foreach (var dir in Directory.GetDirectories(phpRoot))
        {
            string name = Path.GetFileName(dir);
            if (string.Equals(name, "current", StringComparison.OrdinalIgnoreCase)) continue;

            if (File.Exists(Path.Combine(dir, "php-cgi.exe")))
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

            string phpRoot = Path.Combine(appRoot, "bin", "php");
            if (!Directory.Exists(phpRoot)) return false;

            string targetDir = Path.Combine(phpRoot, activeVersion);
            if (!Directory.Exists(targetDir)) return false;

            string junctionPath = Path.Combine(phpRoot, "current");

            // Check if existing junction already points to targetDir
            try
            {
                var dirInfo = new DirectoryInfo(junctionPath);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    string? currentTarget = dirInfo.LinkTarget;
                    if (!string.IsNullOrEmpty(currentTarget) &&
                        string.Equals(Path.GetFullPath(currentTarget), Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }

            // Clean up any existing junction or dangling reparse point
            try
            {
                if (Directory.Exists(junctionPath) || File.Exists(junctionPath))
                {
                    Directory.Delete(junctionPath);
                }
            }
            catch { }

            // Ensure removed via rmdir if still lingering (e.g. broken junction)
            var psiRm = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c rmdir \"{junctionPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var procRm = Process.Start(psiRm);
            procRm?.WaitForExit(2000);

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

    public string GetPhpDirectory()
    {
        string phpDir = Path.Combine(AppRoot, "bin", "php", _config.ActivePhp);
        if (!Directory.Exists(phpDir))
        {
            // fallback: find any folder under bin/php
            string rootPhp = Path.Combine(AppRoot, "bin", "php");
            if (Directory.Exists(rootPhp))
            {
                var dirs = Directory.GetDirectories(rootPhp)
                    .Where(d => !string.Equals(Path.GetFileName(d), "current", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (dirs.Length > 0)
                {
                    return dirs[0];
                }
            }
        }
        return phpDir;
    }

    public string GetPhpCgiPath() => Path.Combine(GetPhpDirectory(), "php-cgi.exe");

    public override async Task<bool> StartAsync()
    {
        LastError = null;
        string phpCgi = GetPhpCgiPath();
        if (!File.Exists(phpCgi))
        {
            LastError = $"php-cgi.exe not found at: {phpCgi}";
            Status = ServiceStatus.Error;
            return false;
        }

        for (int i = 0; i < WorkerCount; i++)
        {
            int p = Port + i;
            if (!await CheckAndResolvePortAsync(p, resolved => _config.PhpFastCgiPort = resolved - i))
            {
                return false;
            }
        }

        Status = ServiceStatus.Starting;
        _supervisorCts = new CancellationTokenSource();

        try
        {
            EnsureConfig(GetPhpDirectory());
            StartPrimaryWorkers();
            _ = Task.Run(() => SupervisorLoopAsync(_supervisorCts.Token));

            await Task.Delay(300);

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                LastError = "php-cgi process exited immediately. Check if VC++ Redistributable is installed.";
                Status = ServiceStatus.Error;
                return false;
            }

            // Start secondary PHP workers for configured sites
            await EnsureWorkersForConfiguredSitesAsync();

            Status = ServiceStatus.Running;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Status = ServiceStatus.Error;
            return false;
        }
    }

    public async Task EnsureWorkersForConfiguredSitesAsync()
    {
        var sites = VirtualHostManager.GetAllSites(AppRoot);
        var requiredVersions = sites
            .Select(s => s.PhpVersion)
            .Where(v => !string.IsNullOrWhiteSpace(v) &&
                        !v.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                        !v.Equals(_config.ActivePhp, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Terminate any worker pool whose version is no longer needed by any site
        lock (_extraWorkers)
        {
            var unneeded = _extraWorkers.Keys.Where(v => !requiredVersions.Contains(v)).ToList();
            foreach (var version in unneeded)
            {
                if (_extraWorkers.Remove(version, out var worker))
                {
                    try
                    {
                        worker.Cts.Cancel();
                        lock (worker.Processes)
                        {
                            foreach (var proc in worker.Processes)
                            {
                                try
                                {
                                    if (proc != null && !proc.HasExited)
                                    {
                                        proc.Kill(true);
                                    }
                                }
                                catch { }
                            }
                            worker.Processes.Clear();
                        }
                    }
                    catch { }
                }
            }
        }

        // 2. Start workers that are actively needed
        foreach (var version in requiredVersions)
        {
            await EnsureWorkerForVersionAsync(version);
        }
    }

    public async Task<bool> EnsureWorkerForVersionAsync(string version)
    {
        if (string.IsNullOrWhiteSpace(version) ||
            version.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            version.Equals(_config.ActivePhp, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        lock (_extraWorkers)
        {
            if (_extraWorkers.TryGetValue(version, out var existing) &&
                existing.Processes.Any(p => p != null && !p.HasExited))
            {
                return true;
            }
        }

        string versionDir = Path.Combine(AppRoot, "bin", "php", version);
        string phpCgi = Path.Combine(versionDir, "php-cgi.exe");
        if (!File.Exists(phpCgi))
        {
            return false;
        }

        int basePort = GetPortForVersion(version, Port);
        EnsureConfig(versionDir);

        var worker = new PhpWorkerInfo
        {
            Version = version,
            BasePort = basePort
        };

        StartExtraWorkers(worker);

        lock (_extraWorkers)
        {
            _extraWorkers[version] = worker;
        }

        _ = Task.Run(() => ExtraWorkerSupervisorLoopAsync(worker, worker.Cts.Token));
        await Task.Delay(200);
        return worker.Processes.Any(p => p != null && !p.HasExited);
    }

    private void EnsureConfig(string phpDir)
    {
        string phpIni = Path.Combine(phpDir, "php.ini");
        string templatePath = Path.Combine(AppRoot, "templates", "php.ini.tpl");

        // If php.ini does not exist, compile from template if available, otherwise configure from development ini
        if (!File.Exists(phpIni))
        {
            if (File.Exists(templatePath))
            {
                string extDir = Path.Combine(phpDir, "ext");
                var variables = TemplateEngine.CreateVariables(AppRoot, _config, extDir);
                TemplateEngine.ProcessTemplate(templatePath, phpIni, variables);
            }
            else
            {
                DevLiteServer.Core.Downloader.PackageDownloader.ConfigurePhpIni(phpDir);
            }
        }
        else
        {
            DevLiteServer.Core.Downloader.PackageDownloader.EnsureOpCacheEnabled(phpIni);
        }
    }

    private void StartPrimaryWorkers()
    {
        string phpCgi = GetPhpCgiPath();
        string phpDir = GetPhpDirectory();

        lock (_primaryWorkers)
        {
            while (_primaryWorkers.Count < WorkerCount)
            {
                _primaryWorkers.Add(null);
            }

            for (int i = 0; i < WorkerCount; i++)
            {
                if (_primaryWorkers[i] == null || _primaryWorkers[i]!.HasExited)
                {
                    _primaryWorkers[i] = LaunchPhpWorker(phpCgi, phpDir, Port + i, i == 0);
                }
            }

            CurrentProcess = _primaryWorkers.FirstOrDefault(p => p != null && !p.HasExited);
        }
    }

    private void StartExtraWorkers(PhpWorkerInfo worker)
    {
        string versionDir = Path.Combine(AppRoot, "bin", "php", worker.Version);
        string phpCgi = Path.Combine(versionDir, "php-cgi.exe");

        lock (worker.Processes)
        {
            while (worker.Processes.Count < WorkerCount)
            {
                worker.Processes.Add(null);
            }

            for (int i = 0; i < WorkerCount; i++)
            {
                if (worker.Processes[i] == null || worker.Processes[i]!.HasExited)
                {
                    worker.Processes[i] = LaunchPhpWorker(phpCgi, versionDir, worker.BasePort + i, false);
                }
            }
        }
    }

    private Process? LaunchPhpWorker(string phpCgi, string workingDir, int port, bool setAsCurrent = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = phpCgi,
            Arguments = $"-b 127.0.0.1:{port}",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        psi.EnvironmentVariables["PHP_FCGI_MAX_REQUESTS"] = "5000";

        return LaunchProcess(psi, setAsCurrent);
    }

    private async Task SupervisorLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (Status == ServiceStatus.Running)
            {
                string phpCgi = GetPhpCgiPath();
                string phpDir = GetPhpDirectory();

                lock (_primaryWorkers)
                {
                    for (int i = 0; i < WorkerCount; i++)
                    {
                        if (i >= _primaryWorkers.Count)
                        {
                            _primaryWorkers.Add(null);
                        }

                        if (_primaryWorkers[i] == null || _primaryWorkers[i]!.HasExited)
                        {
                            _primaryWorkers[i] = LaunchPhpWorker(phpCgi, phpDir, Port + i, i == 0);
                        }
                    }

                    CurrentProcess = _primaryWorkers.FirstOrDefault(p => p != null && !p.HasExited);
                }
            }
            try
            {
                await Task.Delay(1000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task ExtraWorkerSupervisorLoopAsync(PhpWorkerInfo worker, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (Status == ServiceStatus.Running)
            {
                string versionDir = Path.Combine(AppRoot, "bin", "php", worker.Version);
                string phpCgi = Path.Combine(versionDir, "php-cgi.exe");

                lock (worker.Processes)
                {
                    for (int i = 0; i < WorkerCount; i++)
                    {
                        if (i >= worker.Processes.Count)
                        {
                            worker.Processes.Add(null);
                        }

                        if (worker.Processes[i] == null || worker.Processes[i]!.HasExited)
                        {
                            worker.Processes[i] = LaunchPhpWorker(phpCgi, versionDir, worker.BasePort + i, false);
                        }
                    }
                }
            }
            try
            {
                await Task.Delay(1000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public override async Task<bool> StopAsync()
    {
        Status = ServiceStatus.Stopping;
        _supervisorCts?.Cancel();

        var portsToWait = new List<int>();
        for (int i = 0; i < WorkerCount; i++)
        {
            portsToWait.Add(Port + i);
        }

        // 1. Terminate extra workers
        lock (_extraWorkers)
        {
            foreach (var worker in _extraWorkers.Values)
            {
                for (int i = 0; i < WorkerCount; i++)
                {
                    portsToWait.Add(worker.BasePort + i);
                }

                try
                {
                    worker.Cts.Cancel();
                    lock (worker.Processes)
                    {
                        foreach (var proc in worker.Processes)
                        {
                            try
                            {
                                if (proc != null && !proc.HasExited)
                                {
                                    proc.Kill(true);
                                }
                            }
                            catch { }
                        }
                        worker.Processes.Clear();
                    }
                }
                catch { }
            }
            _extraWorkers.Clear();
        }

        // 2. Terminate primary workers
        lock (_primaryWorkers)
        {
            foreach (var proc in _primaryWorkers)
            {
                try
                {
                    if (proc != null && !proc.HasExited)
                    {
                        proc.Kill(true);
                    }
                }
                catch { }
            }
            _primaryWorkers.Clear();
            CurrentProcess = null;
        }

        // 3. Force-kill any lingering/orphaned php-cgi processes inside LiteServer bin/php
        KillLingeringPhpProcesses();

        // 4. Await port release for all workers so new PHP instances don't encounter port collisions
        foreach (var p in portsToWait.Distinct())
        {
            await WaitForPortReleaseAsync(p, 2000);
        }

        Status = ServiceStatus.Stopped;
        return true;
    }

    private void KillLingeringPhpProcesses()
    {
        try
        {
            string phpBinRoot = Path.Combine(AppRoot, "bin", "php");
            foreach (var proc in Process.GetProcessesByName("php-cgi"))
            {
                try
                {
                    string? exePath = proc.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath) && exePath.StartsWith(phpBinRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        proc.Kill(true);
                        proc.WaitForExit(1000);
                    }
                }
                catch
                {
                    try { proc.Kill(true); } catch { }
                }
            }
        }
        catch { }
    }

    private static async Task WaitForPortReleaseAsync(int port, int timeoutMs = 2000)
    {
        int elapsed = 0;
        while (PortChecker.IsPortOccupied(port) && elapsed < timeoutMs)
        {
            await Task.Delay(100);
            elapsed += 100;
        }
    }
}
