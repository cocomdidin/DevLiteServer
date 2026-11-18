using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class RustFsService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "RustFS";
    public override int Port => _config.RustFsApiPort;
    public int ConsolePort => _config.RustFsConsolePort;
    public override bool IsInstalled => File.Exists(GetRustFsExe());

    public override void UpdatePort(int newPort)
    {
        _config.RustFsApiPort = newPort;
    }

    public RustFsService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
        CheckInstallation();
    }

    public string GetRustFsExe()
    {
        string rustfsDir = Path.Combine(AppRoot, "bin", "rustfs");
        string direct = Path.Combine(rustfsDir, "rustfs.exe");
        if (File.Exists(direct)) return direct;

        if (Directory.Exists(rustfsDir))
        {
            var files = Directory.GetFiles(rustfsDir, "rustfs.exe", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return direct;
    }

    public string GetDataDirectory() => Path.Combine(AppRoot, "bin", "rustfs", "data");

    public override async Task<bool> StartAsync()
    {
        LastError = null;
        string exe = GetRustFsExe();
        if (!File.Exists(exe))
        {
            LastError = $"rustfs.exe not found at: {exe}";
            Status = ServiceStatus.Error;
            return false;
        }

        if (!await CheckAndResolvePortAsync(_config.RustFsApiPort, p => _config.RustFsApiPort = p))
        {
            return false;
        }

        if (!await CheckAndResolvePortAsync(_config.RustFsConsolePort, p => _config.RustFsConsolePort = p))
        {
            return false;
        }

        string dataDir = GetDataDirectory();
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        Status = ServiceStatus.Starting;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--address 127.0.0.1:{_config.RustFsApiPort} --console-enable --console-address 127.0.0.1:{_config.RustFsConsolePort} \"{dataDir}\"",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? AppRoot,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            psi.EnvironmentVariables["RUSTFS_ACCESS_KEY"] = _config.RustFsAccessKey;
            psi.EnvironmentVariables["RUSTFS_SECRET_KEY"] = _config.RustFsSecretKey;

            CurrentProcess = LaunchProcess(psi);
            await Task.Delay(500);

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                LastError = "RustFS process exited unexpectedly on startup.";
                Status = ServiceStatus.Error;
                return false;
            }

            Status = ServiceStatus.Running;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to start RustFS: {ex.Message}";
            Status = ServiceStatus.Error;
            return false;
        }
    }

    public override async Task<bool> StopAsync()
    {
        if (Status == ServiceStatus.Stopped || Status == ServiceStatus.NotInstalled) return true;
        Status = ServiceStatus.Stopping;

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
