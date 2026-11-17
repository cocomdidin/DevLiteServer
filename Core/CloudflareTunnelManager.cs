using System.Diagnostics;
using System.Text.RegularExpressions;
using DevLiteServer.Core.Downloader;

namespace DevLiteServer.Core;

public class TunnelSession
{
    public string SiteDomain { get; set; } = "";
    public string TargetUrl { get; set; } = "";
    public string? PublicUrl { get; set; }
    public Process? Process { get; set; }
    public bool IsActive => Process != null && !Process.HasExited;
    public string Status { get; set; } = "Starting...";
    public event Action<string>? LogReceived;
    public event Action<string>? UrlDiscovered;

    internal void NotifyLog(string line) => LogReceived?.Invoke(line);
    internal void NotifyUrl(string url)
    {
        PublicUrl = url;
        Status = "Online";
        UrlDiscovered?.Invoke(url);
    }
}

public static class CloudflareTunnelManager
{
    private static readonly Dictionary<string, TunnelSession> ActiveTunnels = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex TunnelUrlRegex = new(@"https://[a-zA-Z0-9-]+\.trycloudflare\.com", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string GetCloudflaredExe(string appRoot)
    {
        string direct = Path.Combine(appRoot, "bin", "cloudflared", "cloudflared.exe");
        if (File.Exists(direct)) return direct;

        string dir = Path.Combine(appRoot, "bin", "cloudflared");
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "cloudflared.exe", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return direct;
    }

    public static bool IsInstalled(string appRoot) => File.Exists(GetCloudflaredExe(appRoot));

    public static TunnelSession? GetSession(string domain)
    {
        lock (ActiveTunnels)
        {
            return ActiveTunnels.TryGetValue(domain, out var session) ? session : null;
        }
    }

    public static bool IsTunnelActive(string domain)
    {
        var session = GetSession(domain);
        return session != null && session.IsActive;
    }

    public static async Task<TunnelSession> StartTunnelAsync(string appRoot, JobObject job, SiteItem site, AppConfig config)
    {
        string exe = GetCloudflaredExe(appRoot);
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException($"cloudflared.exe not found. Please install it first.");
        }

        StopTunnel(site.Domain);

        // Tunnel targets the local Nginx domain on HttpPort or target proxy port
        string targetHost = $"http://127.0.0.1:{config.HttpPort}";
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"tunnel --url {targetHost} --http-host-header {site.Domain} --no-autoupdate",
            WorkingDirectory = Path.GetDirectoryName(exe) ?? appRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var session = new TunnelSession
        {
            SiteDomain = site.Domain,
            TargetUrl = targetHost,
            Status = "Connecting..."
        };

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        void HandleOutput(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            session.NotifyLog(text);

            var match = TunnelUrlRegex.Match(text);
            if (match.Success && string.IsNullOrEmpty(session.PublicUrl))
            {
                session.NotifyUrl(match.Value);
            }
        }

        proc.OutputDataReceived += (s, e) => HandleOutput(e.Data);
        proc.ErrorDataReceived += (s, e) => HandleOutput(e.Data);

        proc.Start();
        job.AssignProcess(proc);
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        session.Process = proc;

        lock (ActiveTunnels)
        {
            ActiveTunnels[site.Domain] = session;
        }

        proc.Exited += (s, e) =>
        {
            session.Status = "Stopped";
            lock (ActiveTunnels)
            {
                if (ActiveTunnels.TryGetValue(site.Domain, out var active) && active.Process == proc)
                {
                    ActiveTunnels.Remove(site.Domain);
                }
            }
        };

        // Wait up to 10 seconds for URL to appear
        for (int i = 0; i < 50; i++)
        {
            if (!string.IsNullOrEmpty(session.PublicUrl) || proc.HasExited) break;
            await Task.Delay(200);
        }

        return session;
    }

    public static void StopTunnel(string domain)
    {
        lock (ActiveTunnels)
        {
            if (ActiveTunnels.TryGetValue(domain, out var session))
            {
                try
                {
                    if (session.Process != null && !session.Process.HasExited)
                    {
                        session.Process.Kill(true);
                    }
                }
                catch { }
                finally
                {
                    ActiveTunnels.Remove(domain);
                }
            }
        }
    }

    public static void StopAllTunnels()
    {
        lock (ActiveTunnels)
        {
            foreach (var kvp in ActiveTunnels.ToList())
            {
                try
                {
                    if (kvp.Value.Process != null && !kvp.Value.Process.HasExited)
                    {
                        kvp.Value.Process.Kill(true);
                    }
                }
                catch { }
            }
            ActiveTunnels.Clear();
        }
    }
}
