using System.Diagnostics;
using LiteServer.Core;

namespace LiteServer.Services;

public class NginxService : BaseService
{
    private readonly AppConfig _config;
    public PhpService? DependentPhpService { get; set; }

    public override string Name => "Nginx";
    public override int Port => _config.HttpPort;

    public NginxService(string appRoot, JobObject job, AppConfig config, PhpService? phpService = null)
        : base(appRoot, job)
    {
        _config = config;
        DependentPhpService = phpService;
    }

    public string GetNginxDirectory()
    {
        string nginxDir = Path.Combine(AppRoot, "bin", "nginx");
        if (!Directory.Exists(nginxDir)) return nginxDir;

        foreach (var dir in Directory.GetDirectories(nginxDir))
        {
            if (File.Exists(Path.Combine(dir, "nginx.exe")))
            {
                return dir;
            }
        }

        if (File.Exists(Path.Combine(nginxDir, "nginx.exe")))
        {
            return nginxDir;
        }

        var dirs = Directory.GetDirectories(nginxDir);
        return dirs.Length > 0 ? dirs[0] : nginxDir;
    }

    public string GetNginxExe() => Path.Combine(GetNginxDirectory(), "nginx.exe");

    public override async Task<bool> StartAsync()
    {
        LastError = null;

        // Auto-start upstream PHP FastCGI if assigned and not running
        if (DependentPhpService != null && DependentPhpService.Status != ServiceStatus.Running)
        {
            bool phpStarted = await DependentPhpService.StartAsync();
            if (!phpStarted)
            {
                LastError = $"Upstream PHP failed to start: {DependentPhpService.LastError}";
                Status = ServiceStatus.Error;
                return false;
            }
        }

        string nginxExe = GetNginxExe();
        if (!File.Exists(nginxExe))
        {
            LastError = $"Nginx executable not found: {nginxExe}";
            Status = ServiceStatus.Error;
            return false;
        }

        // Port check
        if (PortChecker.IsPortOccupied(Port))
        {
            LastError = $"Port {Port} is occupied by another process. Please free port {Port} or change port in config.ini.";
            Status = ServiceStatus.Error;
            return false;
        }

        Status = ServiceStatus.Starting;

        try
        {
            // Ensure logs directory exists
            string logsDir = Path.Combine(GetNginxDirectory(), "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            GenerateConfig();

            string prefix = GetNginxDirectory().Replace('\\', '/').TrimEnd('/') + "/";
            var psi = new ProcessStartInfo
            {
                FileName = nginxExe,
                Arguments = $"-p \"{prefix}\" -c \"conf/nginx.conf\"",
                WorkingDirectory = GetNginxDirectory(),
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            CurrentProcess = LaunchProcess(psi);
            await Task.Delay(600);

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                string errorLog = Path.Combine(logsDir, "error.log");
                if (File.Exists(errorLog))
                {
                    var lines = await File.ReadAllLinesAsync(errorLog);
                    if (lines.Length > 0)
                    {
                        LastError = lines[^1]; // Last error line
                    }
                }
                LastError ??= "Nginx process exited unexpectedly on startup.";
                Status = ServiceStatus.Error;
                return false;
            }

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

    private void GenerateConfig()
    {
        string templatePath = Path.Combine(AppRoot, "templates", "nginx.conf.tpl");
        string outputPath = Path.Combine(GetNginxDirectory(), "conf", "nginx.conf");

        if (File.Exists(templatePath))
        {
            var vars = TemplateEngine.CreateStandardVariables(
                AppRoot,
                httpPort: _config.HttpPort,
                phpPort: _config.PhpFastCgiPort,
                mysqlPort: _config.MysqlPort
            );
            TemplateEngine.ProcessTemplate(templatePath, outputPath, vars);
        }
    }

    public override async Task<bool> StopAsync()
    {
        Status = ServiceStatus.Stopping;

        try
        {
            string nginxExe = GetNginxExe();
            if (File.Exists(nginxExe))
            {
                string prefix = GetNginxDirectory().Replace('\\', '/').TrimEnd('/') + "/";
                var stopPsi = new ProcessStartInfo
                {
                    FileName = nginxExe,
                    Arguments = $"-s stop -p \"{prefix}\"",
                    WorkingDirectory = GetNginxDirectory(),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var stopProc = Process.Start(stopPsi);
                if (stopProc != null)
                {
                    await stopProc.WaitForExitAsync();
                }
            }

            if (CurrentProcess != null && !CurrentProcess.HasExited)
            {
                CurrentProcess.Kill(true);
                await CurrentProcess.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            CurrentProcess = null;
            Status = ServiceStatus.Stopped;
        }

        return true;
    }
}
