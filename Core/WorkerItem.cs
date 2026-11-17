using System.Diagnostics;
using System.Text.Json.Serialization;

namespace DevLiteServer.Core;

public class WorkerItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool AutoStart { get; set; } = false;
    public bool RestartOnCrash { get; set; } = false;
    public string? SiteDomain { get; set; }

    [JsonIgnore]
    public Process? Process { get; set; }

    [JsonIgnore]
    public bool IsRunning => Process != null && !Process.HasExited;

    [JsonIgnore]
    public string Status { get; set; } = "Stopped";

    [JsonIgnore]
    public List<string> LogBuffer { get; } = new(500);

    public event Action<string>? LogReceived;
    public event Action<string>? StatusChanged;

    internal void NotifyLog(string line)
    {
        lock (LogBuffer)
        {
            if (LogBuffer.Count >= 500)
            {
                LogBuffer.RemoveAt(0);
            }
            LogBuffer.Add(line);
        }
        LogReceived?.Invoke(line);
    }

    internal void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }
}
