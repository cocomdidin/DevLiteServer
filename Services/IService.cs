namespace DevLiteServer.Services;

public enum ServiceStatus
{
    NotInstalled,
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}

public interface IService
{
    string Name { get; }
    int Port { get; }
    ServiceStatus Status { get; }
    bool IsInstalled { get; }
    string? LastError { get; }
    Func<string, int, int, Task<int?>>? PortConflictResolver { get; set; }

    void UpdatePort(int newPort);
    void CheckInstallation();
    Task<bool> StartAsync();
    Task<bool> StopAsync();
    Task<bool> RestartAsync();

    event Action<IService, ServiceStatus>? StatusChanged;
}
