using System.Diagnostics;
using LiteServer.Core;

namespace LiteServer.Services;

public class PhpService : BaseService
{
    private readonly AppConfig _config;
    private CancellationTokenSource? _supervisorCts;

    public override string Name => "PHP (FastCGI)";
    public override int Port => _config.PhpFastCgiPort;

    public PhpService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
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
        string phpCgi = GetPhpCgiPath();
        if (!File.Exists(phpCgi))
        {
            Status = ServiceStatus.Error;
            return false;
        }

        Status = ServiceStatus.Starting;
        _supervisorCts = new CancellationTokenSource();

        try
        {
            StartWorker();
            _ = Task.Run(() => SupervisorLoopAsync(_supervisorCts.Token));

            await Task.Delay(300);
            Status = ServiceStatus.Running;
            return true;
        }
        catch
        {
            Status = ServiceStatus.Error;
            return false;
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

    public override async Task<bool> StopAsync()
    {
        Status = ServiceStatus.Stopping;
        _supervisorCts?.Cancel();

        try
        {
            if (CurrentProcess != null && !CurrentProcess.HasExited)
            {
                CurrentProcess.Kill(true);
                await CurrentProcess.WaitForExitAsync();
            }
        }
        catch
        {
            // Ignore error on kill
        }
        finally
        {
            CurrentProcess = null;
            Status = ServiceStatus.Stopped;
        }

        return true;
    }
}
