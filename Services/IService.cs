namespace LiteServer.Services;

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

    Task<bool> StartAsync();
    Task<bool> StopAsync();
    Task<bool> RestartAsync();

    event Action<IService, ServiceStatus>? StatusChanged;
}
