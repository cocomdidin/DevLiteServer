using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class PostgreSqlService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "PostgreSQL";
    public override int Port => _config.PostgreSqlPort;

    public PostgreSqlService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
    }

    public string GetPostgreSqlDirectory()
    {
        string pgDir = Path.Combine(AppRoot, "bin", "postgresql");
        if (!Directory.Exists(pgDir)) return pgDir;

        foreach (var dir in Directory.GetDirectories(pgDir))
        {
            if (File.Exists(Path.Combine(dir, "bin", "postgres.exe")))
            {
                return dir;
            }
        }

        if (File.Exists(Path.Combine(pgDir, "bin", "postgres.exe")))
        {
            return pgDir;
        }

        var dirs = Directory.GetDirectories(pgDir);
        return dirs.Length > 0 ? dirs[0] : pgDir;
    }

    public string GetDaemonPath() => Path.Combine(GetPostgreSqlDirectory(), "bin", "postgres.exe");
    public string GetInitDbPath() => Path.Combine(GetPostgreSqlDirectory(), "bin", "initdb.exe");
    public string GetDataDirectory() => Path.Combine(GetPostgreSqlDirectory(), "data");

    public override async Task<bool> StartAsync()
    {
        LastError = null;
        string daemon = GetDaemonPath();
        if (!File.Exists(daemon))
        {
            LastError = $"postgres.exe not found at: {daemon}";
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
            await EnsureDatabaseInitializedAsync();

            var psi = new ProcessStartInfo
            {
                FileName = daemon,
                Arguments = $"-D \"{GetDataDirectory()}\" -p {Port}",
                WorkingDirectory = GetPostgreSqlDirectory(),
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            CurrentProcess = LaunchProcess(psi);
            await Task.Delay(1000);

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                LastError = "PostgreSQL server exited unexpectedly on startup.";
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

    private async Task EnsureDatabaseInitializedAsync()
    {
        string dataDir = GetDataDirectory();
        if (Directory.Exists(dataDir) && Directory.GetFileSystemEntries(dataDir).Length > 0)
        {
            return;
        }

        Directory.CreateDirectory(dataDir);
        string initDb = GetInitDbPath();
        if (!File.Exists(initDb)) return;

        var psi = new ProcessStartInfo
        {
            FileName = initDb,
            Arguments = $"-D \"{dataDir}\" -U postgres -A trust --encoding=UTF8",
            WorkingDirectory = GetPostgreSqlDirectory(),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var initProcess = Process.Start(psi);
        if (initProcess != null)
        {
            await initProcess.WaitForExitAsync();
        }
    }

    public override async Task<bool> StopAsync()
    {
        Status = ServiceStatus.Stopping;

        try
        {
            string pgCtl = Path.Combine(GetPostgreSqlDirectory(), "bin", "pg_ctl.exe");
            if (File.Exists(pgCtl))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pgCtl,
                    Arguments = $"stop -D \"{GetDataDirectory()}\" -m fast",
                    WorkingDirectory = GetPostgreSqlDirectory(),
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var stopProc = Process.Start(psi);
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
