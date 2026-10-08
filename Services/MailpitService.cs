using System.Diagnostics;
using LiteServer.Core;

namespace LiteServer.Services;

public class MailpitService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "Mailpit";
    public override int Port => _config.MailpitWebPort;

    public MailpitService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
    }

    public string GetMailpitExe() => Path.Combine(AppRoot, "bin", "mailpit", "mailpit.exe");

    public override async Task<bool> StartAsync()
    {
        string exe = GetMailpitExe();
        if (!File.Exists(exe))
        {
            Status = ServiceStatus.Error;
            return false;
        }

        Status = ServiceStatus.Starting;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--listen 127.0.0.1:{_config.MailpitWebPort} --smtp 127.0.0.1:{_config.MailpitSmtpPort}",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? AppRoot,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            CurrentProcess = LaunchProcess(psi);
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

    public override async Task<bool> StopAsync()
    {
        Status = ServiceStatus.Stopping;

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
            // Ignore error
        }
        finally
        {
            CurrentProcess = null;
            Status = ServiceStatus.Stopped;
        }

        return true;
    }
}
