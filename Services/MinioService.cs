using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class MinioService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "MinIO";
    public override int Port => _config.MinioApiPort;
    public int ConsolePort => _config.MinioConsolePort;
    public override bool IsInstalled => File.Exists(GetMinioExe());

    public override void UpdatePort(int newPort)
    {
        _config.MinioApiPort = newPort;
    }

    public MinioService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
        CheckInstallation();
    }

    public string GetMinioExe()
    {
        string direct = Path.Combine(AppRoot, "bin", "minio", "minio.exe");
        if (File.Exists(direct)) return direct;
        string minioDir = Path.Combine(AppRoot, "bin", "minio");
        if (Directory.Exists(minioDir))
        {
            var files = Directory.GetFiles(minioDir, "minio.exe", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return direct;
    }

    public string GetDataDirectory() => Path.Combine(AppRoot, "bin", "minio", "data");

    public override async Task<bool> StartAsync()
    {
        LastError = null;
        string exe = GetMinioExe();
        if (!File.Exists(exe))
        {
            LastError = $"minio.exe not found at: {exe}";
            Status = ServiceStatus.Error;
            return false;
        }

        if (!await CheckAndResolvePortAsync(_config.MinioApiPort, p => _config.MinioApiPort = p))
        {
            return false;
        }

        if (!await CheckAndResolvePortAsync(_config.MinioConsolePort, p => _config.MinioConsolePort = p))
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
                Arguments = $"server \"{dataDir}\" --address 127.0.0.1:{_config.MinioApiPort} --console-address 127.0.0.1:{_config.MinioConsolePort}",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? AppRoot,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            psi.EnvironmentVariables["MINIO_ROOT_USER"] = _config.MinioRootUser;
            psi.EnvironmentVariables["MINIO_ROOT_PASSWORD"] = _config.MinioRootPassword;

            CurrentProcess = LaunchProcess(psi);
            await Task.Delay(500);

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                LastError = "MinIO process exited unexpectedly on startup.";
                Status = ServiceStatus.Error;
                return false;
            }

            Status = ServiceStatus.Running;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to start MinIO: {ex.Message}";
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
