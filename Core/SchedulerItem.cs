using System.Text.Json.Serialization;

namespace DevLiteServer.Core;

public class SchedulerItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Command { get; set; } = "php artisan schedule:run";
    public string WorkingDirectory { get; set; } = "";
    public string SiteId { get; set; } = "";
    public int IntervalSeconds { get; set; } = 60;
    public bool AutoStart { get; set; } = false;

    [JsonIgnore]
    public bool IsActive { get; set; }

    [JsonIgnore]
    public bool IsRunning { get; set; }

    [JsonIgnore]
    public DateTime? LastRunAt { get; set; }

    [JsonIgnore]
    public DateTime? NextRunAt { get; set; }

    [JsonIgnore]
    public int? LastExitCode { get; set; }

    [JsonIgnore]
    public long LastDurationMs { get; set; }

    [JsonIgnore]
    public List<string> Logs { get; } = new();

    [JsonIgnore]
    public object LogLock { get; } = new();

    private const int MaxLogLines = 300;

    public void AppendLog(string line)
    {
        lock (LogLock)
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            if (Logs.Count > MaxLogLines)
            {
                Logs.RemoveRange(0, Logs.Count - MaxLogLines);
            }
        }
    }

    public string GetLogsText()
    {
        lock (LogLock)
        {
            return string.Join(Environment.NewLine, Logs);
        }
    }
}
