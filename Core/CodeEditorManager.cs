using System.Diagnostics;

namespace DevLiteServer.Core;

public static class CodeEditorManager
{
    public static readonly (string Id, string DisplayName, string Command)[] SupportedEditors = [
        ("vscode", "Visual Studio Code", "code"),
        ("cursor", "Cursor", "cursor"),
        ("antigravity", "Antigravity", "antigravity-ide"),
        ("phpstorm", "PhpStorm", "phpstorm64.exe"),
        ("custom", "Custom Executable...", "")
    ];

    public static string GetEditorDisplayName(string editorId)
    {
        var found = SupportedEditors.FirstOrDefault(e => e.Id.Equals(editorId, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrEmpty(found.DisplayName) ? found.DisplayName : "Code Editor";
    }

    public static bool OpenFolder(AppConfig config, string folderPath, IWin32Window? owner = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            MessageBox.Show(owner, $"Directory not found:\n{folderPath}", "Code Editor Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        string editorId = config.PreferredEditor ?? "vscode";
        string customPath = config.CustomEditorPath ?? "";

        // 1. Custom executable
        if (editorId.Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(customPath) || !File.Exists(customPath))
            {
                var prompt = MessageBox.Show(owner,
                    "Custom code editor path is not configured or executable not found.\n\nWould you like to select your editor executable now?",
                    "Configure Code Editor",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (prompt == DialogResult.Yes)
                {
                    using var ofd = new OpenFileDialog
                    {
                        Title = "Select Code Editor Executable",
                        Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                        CheckFileExists = true
                    };
                    if (ofd.ShowDialog(owner) == DialogResult.OK)
                    {
                        config.CustomEditorPath = ofd.FileName;
                        ConfigManager.Save(Path.Combine(AppPaths.ResolveRoot(), "config.ini"), config);
                        customPath = ofd.FileName;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return LaunchProcess(customPath, $"\"{folderPath}\"", owner);
        }

        // 2. Try direct GUI executable first (cleanest & no console window popup)
        string? resolvedExe = ResolveKnownEditorPath(editorId);
        if (!string.IsNullOrEmpty(resolvedExe) && File.Exists(resolvedExe))
        {
            return LaunchProcess(resolvedExe, $"\"{folderPath}\"", owner);
        }

        // 3. Fallback to CLI command in PATH with hidden window
        var editor = SupportedEditors.FirstOrDefault(e => e.Id.Equals(editorId, StringComparison.OrdinalIgnoreCase));
        string cmd = !string.IsNullOrEmpty(editor.Command) ? editor.Command : "code";

        // Try direct launch via shell with hidden window (prevents batch/cmd console flash)
        if (LaunchProcess(cmd, $"\"{folderPath}\"", null, hideWindow: true))
        {
            return true;
        }

        if (editorId.Equals("antigravity", StringComparison.OrdinalIgnoreCase) && LaunchProcess("antigravity", $"\"{folderPath}\"", null, hideWindow: true))
        {
            return true;
        }

        MessageBox.Show(owner,
            $"Could not launch {editor.DisplayName ?? editorId}.\n\nMake sure it is installed and added to PATH, or configure a custom editor in Settings.",
            "Code Editor Not Found",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);

        return false;
    }

    private static bool LaunchProcess(string fileName, string args, IWin32Window? owner, bool hideWindow = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true
            };

            if (hideWindow)
            {
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            if (owner != null)
            {
                MessageBox.Show(owner, $"Failed to launch {fileName}:\n{ex.Message}", "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }
    }

    private static string? ResolveKnownEditorPath(string editorId)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        switch (editorId.ToLowerInvariant())
        {
            case "vscode":
                return ProbeFiles([
                    Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
                    Path.Combine(programFiles, "Microsoft VS Code", "Code.exe")
                ]);

            case "cursor":
                return ProbeFiles([
                    Path.Combine(localAppData, "Programs", "cursor", "Cursor.exe"),
                    Path.Combine(localAppData, "cursor", "Cursor.exe")
                ]);

            case "antigravity":
                return ProbeFiles([
                    Path.Combine(localAppData, "Programs", "Antigravity IDE", "Antigravity IDE.exe"),
                    Path.Combine(localAppData, "Programs", "Antigravity", "Antigravity.exe"),
                    Path.Combine(programFiles, "Antigravity IDE", "Antigravity IDE.exe"),
                    Path.Combine(programFiles, "Antigravity", "Antigravity.exe")
                ]);

            case "phpstorm":
                try
                {
                    string tbDir = Path.Combine(localAppData, "JetBrains", "Toolbox", "apps", "PhpStorm");
                    if (Directory.Exists(tbDir))
                    {
                        var exe = Directory.GetFiles(tbDir, "phpstorm64.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (exe != null) return exe;
                    }

                    string jbPf = Path.Combine(programFiles, "JetBrains");
                    if (Directory.Exists(jbPf))
                    {
                        var exe = Directory.GetFiles(jbPf, "phpstorm64.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (exe != null) return exe;
                    }
                }
                catch { }
                return null;

            default:
                return null;
        }
    }

    private static string? ProbeFiles(string[] paths)
    {
        return paths.FirstOrDefault(File.Exists);
    }
}
