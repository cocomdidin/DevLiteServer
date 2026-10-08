using System.Diagnostics;
using LiteServer.Core;

namespace LiteServer.Services;

public class NginxService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "Nginx";
    public override int Port => _config.HttpPort;

    public NginxService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
    }

    public string GetNginxDirectory()
    {
        string nginxDir = Path.Combine(AppRoot, "bin", "nginx");
        var dirs = Directory.Exists(nginxDir) ? Directory.GetDirectories(nginxDir) : Array.Empty<string>();
        return dirs.Length > 0 ? dirs[0] : nginxDir;
    }

    public string GetNginxExe() => Path.Combine(GetNginxDirectory(), "nginx.exe");

    public override async Task<bool> StartAsync()
    {
        string nginxExe = GetNginxExe();
        if (!File.Exists(nginxExe))
        {
            Status = ServiceStatus.Error;
            return false;
        }

        Status = ServiceStatus.Starting;

        try
        {
            GenerateConfig();

            var psi = new ProcessStartInfo
            {
                FileName = nginxExe,
                Arguments = $"-p \"{GetNginxDirectory()}\" -c \"conf/nginx.conf\"",
                WorkingDirectory = GetNginxDirectory(),
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            CurrentProcess = LaunchProcess(psi);
            await Task.Delay(500);

            Status = ServiceStatus.Running;
            return true;
        }
        catch
        {
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
                var stopPsi = new ProcessStartInfo
                {
                    FileName = nginxExe,
                    Arguments = $"-s stop -p \"{GetNginxDirectory()}\"",
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
        catch
        {
            // Ignore stop errors
        }
        finally
        {
            CurrentProcess = null;
            Status = ServiceStatus.Stopped;
        }

        return true;
    }
}
