using System.Diagnostics;
using System.Text.RegularExpressions;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class PhpWorkerInfo
{
    public string Version { get; set; } = "";
    public int Port { get; set; }
    public Process? Process { get; set; }
    public CancellationTokenSource Cts { get; set; } = new();
}

public class PhpService : BaseService
{
    private readonly AppConfig _config;
    private CancellationTokenSource? _supervisorCts;
    private readonly Dictionary<string, PhpWorkerInfo> _extraWorkers = new(StringComparer.OrdinalIgnoreCase);

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
            return 9000 + (major * 10) + minor; // e.g. 8.4 -> 9084, 8.2 -> 9082, 7.4 -> 9074
        }

        return 9100 + Math.Abs(version.GetHashCode() % 100);
    }

    public static List<string> GetInstalledVersions(string appRoot)
    {
        var list = new List<string>();
        string phpRoot = Path.Combine(appRoot, "bin", "php");
        if (!Directory.Exists(phpRoot)) return list;

        foreach (var dir in Directory.GetDirectories(phpRoot))
        {
            if (File.Exists(Path.Combine(dir, "php-cgi.exe")))
            {
                list.Add(Path.GetFileName(dir));
            }
        }
        return list;
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
                var dirs = Directory.GetDirectories(rootPhp);
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

        if (!await CheckAndResolvePortAsync(Port, p => _config.PhpFastCgiPort = p))
        {
            return false;
        }

        Status = ServiceStatus.Starting;
        _supervisorCts = new CancellationTokenSource();

        try
        {
            EnsureConfig(GetPhpDirectory());
            StartWorker();
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
            .Distinct(StringComparer.OrdinalIgnoreCase);

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
            if (_extraWorkers.TryGetValue(version, out var existing) && existing.Process != null && !existing.Process.HasExited)
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

        int port = GetPortForVersion(version, Port);
        EnsureConfig(versionDir);

        var worker = new PhpWorkerInfo
        {
            Version = version,
            Port = port
        };

        StartExtraWorkerProcess(worker);

        lock (_extraWorkers)
        {
            _extraWorkers[version] = worker;
        }

        _ = Task.Run(() => ExtraWorkerSupervisorLoopAsync(worker, worker.Cts.Token));
        await Task.Delay(200);
        return worker.Process != null && !worker.Process.HasExited;
    }

    private void EnsureConfig(string phpDir)
    {
        string phpIni = Path.Combine(phpDir, "php.ini");
        string templatePath = Path.Combine(AppRoot, "templates", "php.ini.tpl");

        // If php.ini does not exist, compile from template if available
        if (!File.Exists(phpIni) && File.Exists(templatePath))
        {
            string extDir = Path.Combine(phpDir, "ext");
            var variables = TemplateEngine.CreateVariables(AppRoot, _config, extDir);
            TemplateEngine.ProcessTemplate(templatePath, phpIni, variables);
        }
    }

    private void StartWorker()
    {
        string phpCgi = GetPhpCgiPath();
        string phpDir = GetPhpDirectory();

        var psi = new ProcessStartInfo
        {
            FileName = phpCgi,
            Arguments = $"-b 127.0.0.1:{Port}",
            WorkingDirectory = phpDir,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        psi.EnvironmentVariables["PHP_FCGI_MAX_REQUESTS"] = "5000";

        CurrentProcess = LaunchProcess(psi);
    }

    private void StartExtraWorkerProcess(PhpWorkerInfo worker)
    {
        string versionDir = Path.Combine(AppRoot, "bin", "php", worker.Version);
        string phpCgi = Path.Combine(versionDir, "php-cgi.exe");

        var psi = new ProcessStartInfo
        {
            FileName = phpCgi,
            Arguments = $"-b 127.0.0.1:{worker.Port}",
            WorkingDirectory = versionDir,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        psi.EnvironmentVariables["PHP_FCGI_MAX_REQUESTS"] = "5000";

        worker.Process = LaunchProcess(psi);
    }

    private async Task SupervisorLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (Status == ServiceStatus.Running)
            {
                if (CurrentProcess == null || CurrentProcess.HasExited)
                {
                    // Respawn worker when max requests reached
                    StartWorker();
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
                if (worker.Process == null || worker.Process.HasExited)
                {
                    StartExtraWorkerProcess(worker);
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

        // Terminate extra workers
        lock (_extraWorkers)
        {
            foreach (var worker in _extraWorkers.Values)
            {
                try
                {
                    worker.Cts.Cancel();
                    if (worker.Process != null && !worker.Process.HasExited)
                    {
                        worker.Process.Kill(true);
                    }
                }
                catch { }
            }
            _extraWorkers.Clear();
        }

        try
        {
            if (CurrentProcess != null && !CurrentProcess.HasExited)
            {
                CurrentProcess.Kill(true);
                await CurrentProcess.WaitForExitAsync();
            }
        }
        catch { }
        finally
        {
            CurrentProcess = null;
            Status = ServiceStatus.Stopped;
        }

        return true;
    }
}
