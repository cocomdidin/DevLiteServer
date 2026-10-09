using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class MailpitService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "Mailpit";
    public override int Port => _config.MailpitWebPort;
    public override bool IsInstalled => File.Exists(GetMailpitExe());

    public override void UpdatePort(int newPort)
    {
        _config.MailpitWebPort = newPort;
    }

    public MailpitService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
        CheckInstallation();
    }

    public string GetMailpitExe()
    {
        string direct = Path.Combine(AppRoot, "bin", "mailpit", "mailpit.exe");
        if (File.Exists(direct)) return direct;
        string mailpitDir = Path.Combine(AppRoot, "bin", "mailpit");
        if (Directory.Exists(mailpitDir))
        {
            var files = Directory.GetFiles(mailpitDir, "mailpit.exe", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return direct;
    }

    public override async Task<bool> StartAsync()
    {
        LastError = null;
        string exe = GetMailpitExe();
        if (!File.Exists(exe))
        {
            LastError = $"mailpit.exe not found at: {exe}";
            Status = ServiceStatus.Error;
            return false;
        }

        if (!await CheckAndResolvePortAsync(_config.MailpitWebPort, p => _config.MailpitWebPort = p))
        {
            return false;
        }

        if (!await CheckAndResolvePortAsync(_config.MailpitSmtpPort, p => _config.MailpitSmtpPort = p))
        {
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

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                LastError = "Mailpit process exited unexpectedly on startup.";
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
