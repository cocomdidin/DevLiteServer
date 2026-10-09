using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace DevLiteServer.Core;

public record UpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    bool IsUpdateAvailable,
    string ReleaseName,
    string ReleaseNotes,
    string? DownloadUrl,
    string? HtmlUrl,
    long FileSize
);

public static class UpdateChecker
{
    private static readonly HttpClient HttpClient = new();

    static UpdateChecker()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DevLiteServer", GetCurrentVersionString())
        );
        HttpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public static Version GetCurrentVersion()
    {
        var asm = typeof(UpdateChecker).Assembly;
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVersion))
        {
            string clean = infoVersion.Split('+')[0].TrimStart('v', 'V');
            if (Version.TryParse(clean, out var parsedInfo))
            {
                return parsedInfo;
            }
        }

        var ver = asm.GetName().Version;
        return ver != null ? new Version(ver.Major, ver.Minor, Math.Max(ver.Build, 0)) : new Version(1, 0, 0);
    }

    public static string GetCurrentVersionString() => GetCurrentVersion().ToString(3);

    public static async Task<UpdateInfo?> CheckForUpdatesAsync(
        string repoOwner = "cocomdidin",
        string repoName = "LocalLiteServer",
        CancellationToken cancellationToken = default)
    {
        string currentVerStr = GetCurrentVersionString();
        var currentVer = GetCurrentVersion();

        string url = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string rawTagName = root.GetProperty("tag_name").GetString() ?? "";
            string cleanTag = rawTagName.TrimStart('v', 'V');
            string releaseName = root.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString())
                ? n.GetString()!
                : rawTagName;
            string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

            string? downloadUrl = null;
            long fileSize = 0;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        assetName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        fileSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                        break;
                    }
                }

                // Fallback to any .exe asset if setup not explicitly named
                if (downloadUrl == null)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string assetName = asset.GetProperty("name").GetString() ?? "";
                        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            fileSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                            break;
                        }
                    }
                }
            }

            bool isNewer = false;
            if (Version.TryParse(cleanTag, out var latestVer))
            {
                isNewer = latestVer > currentVer;
            }
            else
            {
                isNewer = !string.Equals(cleanTag, currentVerStr, StringComparison.OrdinalIgnoreCase);
            }

            return new UpdateInfo(
                CurrentVersion: currentVerStr,
                LatestVersion: cleanTag,
                IsUpdateAvailable: isNewer,
                ReleaseName: releaseName,
                ReleaseNotes: body,
                DownloadUrl: downloadUrl,
                HtmlUrl: htmlUrl,
                FileSize: fileSize
            );
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> DownloadInstallerAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<int>? progress = null,
        IProgress<string>? statusProgress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    int pct = (int)((totalRead * 100) / totalBytes.Value);
                    progress?.Report(pct);

                    double mbRead = totalRead / (1024.0 * 1024.0);
                    double mbTotal = totalBytes.Value / (1024.0 * 1024.0);
                    statusProgress?.Report($"Downloading update... {pct}% ({mbRead:F1} MB / {mbTotal:F1} MB)");
                }
            }

            return true;
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                try { File.Delete(destinationPath); } catch { }
            }
            return false;
        }
    }
}
