using System.Diagnostics;
using LiteServer.Core;

namespace LiteServer.Services;

public abstract class BaseService : IService
{
    protected readonly string AppRoot;
    protected readonly JobObject Job;
    protected Process? CurrentProcess;

    public abstract string Name { get; }
    public abstract int Port { get; }

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

    public abstract Task<bool> StartAsync();
    public abstract Task<bool> StopAsync();

    public virtual async Task<bool> RestartAsync()
    {
        await StopAsync();
        await Task.Delay(500);
        return await StartAsync();
    }

    protected Process? LaunchProcess(ProcessStartInfo psi)
    {
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;

        var proc = Process.Start(psi);
        if (proc != null)
        {
            Job.AssignProcess(proc);
            CurrentProcess = proc;
        }
        return proc;
    }
}
