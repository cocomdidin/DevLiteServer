using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class RedisService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "Redis";
    public override int Port => _config.RedisPort;

    public RedisService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
    }

    public string GetRedisDirectory()
    {
        string redisDir = Path.Combine(AppRoot, "bin", "redis");
        if (!Directory.Exists(redisDir)) return redisDir;

        foreach (var dir in Directory.GetDirectories(redisDir))
        {
            if (File.Exists(Path.Combine(dir, "redis-server.exe")))
            {
                return dir;
            }
        }

        return redisDir;
    }

    public string GetDaemonPath()
    {
        string dir = GetRedisDirectory();
        string inSubDir = Path.Combine(dir, "redis-server.exe");
        if (File.Exists(inSubDir)) return inSubDir;

        string direct = Path.Combine(AppRoot, "bin", "redis", "redis-server.exe");
        return direct;
    }

    public override async Task<bool> StartAsync()
    {
        LastError = null;
        string daemon = GetDaemonPath();
        if (!File.Exists(daemon))
        {
            LastError = $"redis-server.exe not found at: {daemon}";
            Status = ServiceStatus.Error;
            return false;
        }

        if (PortChecker.IsPortOccupied(Port))
        {
            LastError = $"Port {Port} is occupied by another process.";
            Status = ServiceStatus.Error;
            return false;
        }

        Status = ServiceStatus.Starting;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = daemon,
                Arguments = $"--port {Port} --bind 127.0.0.1",
                WorkingDirectory = Path.GetDirectoryName(daemon) ?? AppRoot,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            CurrentProcess = LaunchProcess(psi);
            await Task.Delay(300);

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                LastError = "Redis server exited unexpectedly on startup.";
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
