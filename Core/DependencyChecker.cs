using System.Diagnostics;

namespace DevLiteServer.Core;

public static class DependencyChecker
{
    private const string VcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

    /// <summary>
    /// Checks if MSVCRT (VCRUNTIME140.dll) is available in System32.
    /// </summary>
    public static bool IsVcRedistInstalled()
    {
        string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string vcRuntimePath = Path.Combine(systemPath, "vcruntime140.dll");
        return File.Exists(vcRuntimePath);
    }

    /// <summary>
    /// Prompts user and downloads/installs VC++ Redistributable if needed.
    /// </summary>
    public static async Task<bool> InstallVcRedistAsync(IProgress<int>? progress = null)
    {
        try
        {
            string tempInstaller = Path.Combine(Path.GetTempPath(), "vc_redist.x64.exe");
            using var client = new HttpClient();
            using var response = await client.GetAsync(VcRedistUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempInstaller, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;
                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((int)((totalRead * 100) / totalBytes));
                }
            }

            fileStream.Close();

            // Run installer with prompt for user
            var psi = new ProcessStartInfo
            {
                FileName = tempInstaller,
                Arguments = "/passive /norestart",
                UseShellExecute = true,
                Verb = "runas"
            };

            var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0 || process.ExitCode == 3010; // 3010 = reboot required
            }
        }
        catch
        {
            // User cancelled or network error
        }
        return false;
    }
}
