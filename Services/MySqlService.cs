using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public class MySqlService : BaseService
{
    private readonly AppConfig _config;

    public override string Name => "MySQL";
    public override int Port => _config.MysqlPort;

    public override void UpdatePort(int newPort)
    {
        _config.MysqlPort = newPort;
    }

    public MySqlService(string appRoot, JobObject job, AppConfig config)
        : base(appRoot, job)
    {
        _config = config;
    }

    public string GetMySqlDirectory()
    {
        string mysqlDir = Path.Combine(AppRoot, "bin", "mysql");
        if (!Directory.Exists(mysqlDir)) return mysqlDir;

        foreach (var dir in Directory.GetDirectories(mysqlDir))
        {
            if (File.Exists(Path.Combine(dir, "bin", "mysqld.exe")))
            {
                return dir;
            }
        }

        if (File.Exists(Path.Combine(mysqlDir, "bin", "mysqld.exe")))
        {
            return mysqlDir;
        }

        var dirs = Directory.GetDirectories(mysqlDir);
        return dirs.Length > 0 ? dirs[0] : mysqlDir;
    }

    public string GetMySqlDaemon() => Path.Combine(GetMySqlDirectory(), "bin", "mysqld.exe");
    public string GetDataDirectory() => Path.Combine(GetMySqlDirectory(), "data");

    public override async Task<bool> StartAsync()
    {
        LastError = null;
        string daemon = GetMySqlDaemon();
        if (!File.Exists(daemon))
        {
            LastError = $"mysqld.exe not found at: {daemon}";
            Status = ServiceStatus.Error;
            return false;
        }

        if (!await CheckAndResolvePortAsync(Port, p => _config.MysqlPort = p))
        {
            return false;
        }

        Status = ServiceStatus.Starting;

        try
        {
            await EnsureDatabaseInitializedAsync();
            GenerateConfig();

            string iniPath = Path.Combine(GetMySqlDirectory(), "my.ini");
            var psi = new ProcessStartInfo
            {
                FileName = daemon,
                Arguments = $"--defaults-file=\"{iniPath}\" --standalone",
                WorkingDirectory = GetMySqlDirectory(),
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            CurrentProcess = LaunchProcess(psi);
            await Task.Delay(1000);

            if (CurrentProcess == null || CurrentProcess.HasExited)
            {
                LastError = "MySQL daemon exited unexpectedly on startup.";
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
        if (!Directory.Exists(dataDir) || Directory.GetFileSystemEntries(dataDir).Length == 0)
        {
            Directory.CreateDirectory(dataDir);
            var initPsi = new ProcessStartInfo
            {
                FileName = GetMySqlDaemon(),
                Arguments = $"--initialize-insecure --datadir=\"{dataDir}\" --basedir=\"{GetMySqlDirectory()}\"",
                WorkingDirectory = GetMySqlDirectory(),
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var initProc = Process.Start(initPsi);
            if (initProc != null)
            {
                await initProc.WaitForExitAsync();
            }
        }
    }

    private void GenerateConfig()
    {
        string templatePath = Path.Combine(AppRoot, "templates", "my.ini.tpl");
        string outputPath = Path.Combine(GetMySqlDirectory(), "my.ini");

        if (File.Exists(templatePath))
        {
            var vars = TemplateEngine.CreateStandardVariables(
                AppRoot,
                httpPort: _config.HttpPort,
                phpPort: _config.PhpFastCgiPort,
                mysqlPort: _config.MysqlPort
            );
            vars["MYSQL_BASEDIR_FORWARD"] = GetMySqlDirectory().Replace('\\', '/');
            vars["MYSQL_DATADIR_FORWARD"] = GetDataDirectory().Replace('\\', '/');
            TemplateEngine.ProcessTemplate(templatePath, outputPath, vars);
        }
    }

    public override async Task<bool> StopAsync()
    {
        Status = ServiceStatus.Stopping;

        try
        {
            string adminExe = Path.Combine(GetMySqlDirectory(), "bin", "mysqladmin.exe");
            if (File.Exists(adminExe))
            {
                var stopPsi = new ProcessStartInfo
                {
                    FileName = adminExe,
                    Arguments = $"-u root --port={Port} shutdown",
                    WorkingDirectory = GetMySqlDirectory(),
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
