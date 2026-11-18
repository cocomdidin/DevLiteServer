using System.Diagnostics;
using System.Text.Json;

namespace DevLiteServer.Core;

public static class SchedulerManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly List<SchedulerItem> _tasks = new();
    private static readonly object _syncLock = new();
    private static CancellationTokenSource? _loopCts;
    private static string _appRoot = "";
    private static JobObject? _job;

    public static event EventHandler<SchedulerItem>? TaskUpdated;

    private static string GetConfigPath(string appRoot) => Path.Combine(appRoot, "data", "schedulers.json");

    public static IReadOnlyList<SchedulerItem> Tasks
    {
        get
        {
            lock (_syncLock)
            {
                return _tasks.ToList();
            }
        }
    }

    public static void Load(string appRoot)
    {
        lock (_syncLock)
        {
            _appRoot = appRoot;
            _tasks.Clear();
            string path = GetConfigPath(appRoot);
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<SchedulerItem>>(json, JsonOptions);
                if (items != null)
                {
                    _tasks.AddRange(items);
                }
            }
            catch { }
        }
    }

    public static void Save(string appRoot)
    {
        lock (_syncLock)
        {
            try
            {
                string path = GetConfigPath(appRoot);
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(_tasks, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }

    public static void AddTask(string appRoot, SchedulerItem item)
    {
        lock (_syncLock)
        {
            _tasks.Add(item);
        }
        Save(appRoot);
    }

    public static void UpdateTask(string appRoot, SchedulerItem updated)
    {
        lock (_syncLock)
        {
            int idx = _tasks.FindIndex(t => t.Id == updated.Id);
            if (idx >= 0)
            {
                var existing = _tasks[idx];
                updated.IsActive = existing.IsActive;
                updated.IsRunning = existing.IsRunning;
                updated.LastRunAt = existing.LastRunAt;
                updated.NextRunAt = existing.IsActive
                    ? DateTime.UtcNow.AddSeconds(Math.Max(5, updated.IntervalSeconds))
                    : null;
                updated.LastExitCode = existing.LastExitCode;
                updated.LastDurationMs = existing.LastDurationMs;
                _tasks[idx] = updated;
            }
        }
        Save(appRoot);
        TaskUpdated?.Invoke(null, updated);
    }

    public static void DeleteTask(string appRoot, string id)
    {
        lock (_syncLock)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t != null)
            {
                t.IsActive = false;
                _tasks.Remove(t);
            }
        }
        Save(appRoot);
    }

    public static void StartTask(string appRoot, JobObject job, SchedulerItem task)
    {
        _appRoot = appRoot;
        _job = job;
        EnsureLoopRunning();

        task.IsActive = true;
        task.NextRunAt = DateTime.UtcNow.AddSeconds(Math.Max(5, task.IntervalSeconds));
        TaskUpdated?.Invoke(null, task);
    }

    public static void StopTask(SchedulerItem task)
    {
        task.IsActive = false;
        task.NextRunAt = null;
        TaskUpdated?.Invoke(null, task);
    }

    public static void StartAutoTasks(string appRoot, JobObject job)
    {
        _appRoot = appRoot;
        _job = job;
        EnsureLoopRunning();

        List<SchedulerItem> autoTasks;
        lock (_syncLock)
        {
            autoTasks = _tasks.Where(t => t.AutoStart).ToList();
        }

        foreach (var task in autoTasks)
        {
            StartTask(appRoot, job, task);
        }
    }

    public static void StopAll()
    {
        _loopCts?.Cancel();
        _loopCts = null;

        List<SchedulerItem> all;
        lock (_syncLock)
        {
            all = _tasks.ToList();
        }

        foreach (var task in all)
        {
            task.IsActive = false;
            task.NextRunAt = null;
            TaskUpdated?.Invoke(null, task);
        }
    }

    private static void EnsureLoopRunning()
    {
        if (_loopCts != null && !_loopCts.IsCancellationRequested) return;
        _loopCts = new CancellationTokenSource();
        var ct = _loopCts.Token;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    List<SchedulerItem> due;
                    DateTime now = DateTime.UtcNow;
                    lock (_syncLock)
                    {
                        due = _tasks
                            .Where(t => t.IsActive && !t.IsRunning && t.NextRunAt.HasValue && now >= t.NextRunAt.Value)
                            .ToList();
                    }

                    foreach (var task in due)
                    {
                        _ = RunNowAsync(_appRoot, _job, task);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }, ct);
    }

    public static async Task RunNowAsync(string appRoot, JobObject? job, SchedulerItem task)
    {
        if (task.IsRunning) return;
        if (string.IsNullOrWhiteSpace(task.WorkingDirectory) || !Directory.Exists(task.WorkingDirectory))
        {
            task.AppendLog($"[ERROR] Working directory not found: {task.WorkingDirectory}");
            task.LastExitCode = -1;
            task.LastRunAt = DateTime.Now;
            if (task.IsActive)
            {
                task.NextRunAt = DateTime.UtcNow.AddSeconds(Math.Max(5, task.IntervalSeconds));
            }
            TaskUpdated?.Invoke(null, task);
            return;
        }

        task.IsRunning = true;
        TaskUpdated?.Invoke(null, task);

        var sw = Stopwatch.StartNew();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {task.Command}",
                WorkingDirectory = task.WorkingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            string currentPath = psi.EnvironmentVariables["PATH"] ?? "";
            string phpCurrent = Path.Combine(appRoot, "bin", "php", "current");
            string composerDir = Path.Combine(appRoot, "bin", "composer");
            string nodeCurrent = Path.Combine(appRoot, "bin", "nodejs", "current");
            psi.EnvironmentVariables["PATH"] = $"{phpCurrent};{composerDir};{nodeCurrent};{currentPath}";

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                task.AppendLog("[ERROR] Failed to start process.");
                task.LastExitCode = -1;
                return;
            }

            job?.AssignProcess(proc);

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    task.AppendLog(e.Data);
            };
            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    task.AppendLog($"[ERR] {e.Data}");
            };

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();
            task.LastExitCode = proc.ExitCode;
        }
        catch (Exception ex)
        {
            task.AppendLog($"[ERROR] {ex.Message}");
            task.LastExitCode = -1;
        }
        finally
        {
            sw.Stop();
            task.LastDurationMs = sw.ElapsedMilliseconds;
            task.LastRunAt = DateTime.Now;
            task.IsRunning = false;
            if (task.IsActive)
            {
                task.NextRunAt = DateTime.UtcNow.AddSeconds(Math.Max(5, task.IntervalSeconds));
            }
            TaskUpdated?.Invoke(null, task);
        }
    }
}
