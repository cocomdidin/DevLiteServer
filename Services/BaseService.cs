using System.Diagnostics;
using DevLiteServer.Core;

namespace DevLiteServer.Services;

public abstract class BaseService : IService
{
    protected readonly string AppRoot;
    protected readonly JobObject Job;
    protected Process? CurrentProcess;

    public abstract string Name { get; }
    public abstract int Port { get; }
    public string? LastError { get; protected set; }
    public Func<string, int, int, Task<int?>>? PortConflictResolver { get; set; }

    public virtual bool IsInstalled => true;

    private ServiceStatus _status = ServiceStatus.Stopped;
    public ServiceStatus Status
    {
        get => _status;
        protected set
        {
            if (_status != value)
            {
                _status = value;
                StatusChanged?.Invoke(this, _status);
            }
        }
    }

    public event Action<IService, ServiceStatus>? StatusChanged;

    protected BaseService(string appRoot, JobObject job)
    {
        AppRoot = appRoot;
        Job = job;
    }

    public virtual void CheckInstallation()
    {
        if (!IsInstalled)
        {
            Status = ServiceStatus.NotInstalled;
        }
        else if (Status == ServiceStatus.NotInstalled)
        {
            Status = ServiceStatus.Stopped;
        }
    }

    public abstract Task<bool> StartAsync();
    public abstract Task<bool> StopAsync();

    public virtual void UpdatePort(int newPort)
    {
    }

    protected async Task<bool> CheckAndResolvePortAsync(int currentPort, Action<int> applyPort)
    {
        // Give port up to 2.5 seconds to be released by OS if recently closed/stopped
        for (int i = 0; i < 25; i++)
        {
            if (!PortChecker.IsPortOccupied(currentPort))
            {
                return true;
            }
            await Task.Delay(100);
        }

        if (PortConflictResolver != null)
        {
            int suggestedBase = currentPort == 80 ? 8080 : currentPort + 1;
            int suggested = PortChecker.GetAvailablePort(suggestedBase);
            int? resolved = await PortConflictResolver(Name, currentPort, suggested);
            if (resolved.HasValue && resolved.Value > 0)
            {
                applyPort(resolved.Value);
                UpdatePort(resolved.Value);
                return true;
            }
        }

        LastError = $"Port {currentPort} is occupied by another process.";
        Status = ServiceStatus.Error;
        return false;
    }

    public virtual async Task<bool> RestartAsync()
    {
        await StopAsync();
        await Task.Delay(200);
        return await StartAsync();
    }

    protected Process? LaunchProcess(ProcessStartInfo psi, bool setAsCurrentProcess = true)
    {
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;

        var proc = Process.Start(psi);
        if (proc != null)
        {
            Job.AssignProcess(proc);
            if (setAsCurrentProcess)
            {
                CurrentProcess = proc;
            }
        }
        return proc;
    }
}
