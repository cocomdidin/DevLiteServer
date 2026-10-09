namespace DevLiteServer.Services;

public enum ServiceStatus
{
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
    string? LastError { get; }
    Func<string, int, int, Task<int?>>? PortConflictResolver { get; set; }

    void UpdatePort(int newPort);
    Task<bool> StartAsync();
    Task<bool> StopAsync();
    Task<bool> RestartAsync();

    event Action<IService, ServiceStatus>? StatusChanged;
}
