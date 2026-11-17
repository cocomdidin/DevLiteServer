using System.Diagnostics;
using System.Text.Json;

namespace DevLiteServer.Core;

public static class WorkerManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly List<WorkerItem> _workers = new();
    private static readonly object _syncLock = new();

    private static string GetConfigPath(string appRoot) => Path.Combine(appRoot, "data", "workers.json");

    public static IReadOnlyList<WorkerItem> Workers
    {
        get
        {
            lock (_syncLock)
            {
                return _workers.ToList();
            }
        }
    }

    public static void Load(string appRoot)
    {
        lock (_syncLock)
        {
            _workers.Clear();
            string path = GetConfigPath(appRoot);
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<WorkerItem>>(json, JsonOptions);
                if (items != null)
                {
                    _workers.AddRange(items);
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
                string json = JsonSerializer.Serialize(_workers, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }

    public static void AddWorker(string appRoot, WorkerItem worker)
    {
        lock (_syncLock)
        {
            _workers.Add(worker);
            Save(appRoot);
        }
    }

    public static void UpdateWorker(string appRoot, WorkerItem worker)
    {
        lock (_syncLock)
        {
            int idx = _workers.FindIndex(w => w.Id == worker.Id);
            if (idx >= 0)
            {
                _workers[idx] = worker;
                Save(appRoot);
            }
        }
    }

    public static void RemoveWorker(string appRoot, string workerId)
    {
        lock (_syncLock)
        {
            var item = _workers.Find(w => w.Id == workerId);
            if (item != null)
            {
                StopWorker(item);
                _workers.Remove(item);
                Save(appRoot);
            }
        }
    }

    public static WorkerItem? GetWorker(string workerId)
    {
        lock (_syncLock)
        {
            return _workers.Find(w => w.Id == workerId);
        }
    }

    public static WorkerItem? FindByDomain(string domain)
    {
        lock (_syncLock)
        {
            return _workers.Find(w => string.Equals(w.SiteDomain, domain, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static bool StartWorker(WorkerItem worker, string appRoot, JobObject job)
    {
        if (worker.IsRunning) return true;

        try
        {
            worker.SetStatus("Starting");
            worker.NotifyLog($"[System] Starting worker: {worker.Name} ({worker.Command})");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {worker.Command}",
                WorkingDirectory = Directory.Exists(worker.WorkingDirectory) ? worker.WorkingDirectory : appRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Inject LiteServer portable paths to PATH environment
            string currentPath = psi.EnvironmentVariables["PATH"] ?? "";
            string phpPath = Path.Combine(appRoot, "bin", "php", "current");
            string nodePath = Path.Combine(appRoot, "bin", "node", "current");
            string composerPath = Path.Combine(appRoot, "bin", "composer");
            string gitPath = Path.Combine(appRoot, "bin", "git", "cmd");

            string injected = $"{phpPath};{nodePath};{composerPath};{gitPath};{currentPath}";
            psi.EnvironmentVariables["PATH"] = injected;

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    worker.NotifyLog(e.Data);
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    worker.NotifyLog(e.Data);
            };

            proc.Exited += (s, e) =>
            {
                int exitCode = -1;
                try { exitCode = proc.ExitCode; } catch { }
                worker.Process = null;

                if (worker.Status != "Stopped")
                {
                    worker.NotifyLog($"[System] Process exited with code {exitCode}");
                    if (exitCode != 0 && worker.RestartOnCrash)
                    {
                        worker.SetStatus("Crashed (Restarting...)");
                        Task.Delay(3000).ContinueWith(_ =>
                        {
                            StartWorker(worker, appRoot, job);
                        });
                        return;
                    }
                    worker.SetStatus(exitCode == 0 ? "Stopped" : "Failed");
                }
            };

            if (proc.Start())
            {
                job.AssignProcess(proc);
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                worker.Process = proc;
                worker.SetStatus("Running");
                worker.NotifyLog($"[System] Process started with PID {proc.Id}");
                return true;
            }
            else
            {
                worker.SetStatus("Failed");
                worker.NotifyLog("[System] Failed to start process");
                return false;
            }
        }
        catch (Exception ex)
        {
            worker.SetStatus("Failed");
            worker.NotifyLog($"[System Error] {ex.Message}");
            return false;
        }
    }

    public static void StopWorker(WorkerItem worker)
    {
        worker.SetStatus("Stopped");
        if (worker.Process != null && !worker.Process.HasExited)
        {
            try
            {
                worker.NotifyLog("[System] Stopping worker process tree...");
                worker.Process.Kill(true);
            }
            catch { }
            finally
            {
                worker.Process = null;
            }
        }
        worker.NotifyLog("[System] Worker stopped");
    }

    public static void StartAllAutoWorkers(string appRoot, JobObject job)
    {
        lock (_syncLock)
        {
            foreach (var w in _workers.Where(w => w.AutoStart))
            {
                StartWorker(w, appRoot, job);
            }
        }
    }

    public static void StopAllWorkers()
    {
        lock (_syncLock)
        {
            foreach (var w in _workers)
            {
                StopWorker(w);
            }
        }
    }
}
