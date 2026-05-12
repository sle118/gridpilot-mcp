using System.Runtime.Versioning;

namespace ExcelMcp.Deployment.Installation;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartMenuShortcutWriter : IStartMenuShortcutWriter
{
    public void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath) ?? throw new InvalidOperationException("Shortcut path is missing a parent folder."));

        var shellType = Type.GetTypeFromProgID("WScript.Shell") ??
            throw new InvalidOperationException("WScript.Shell is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType) ??
            throw new InvalidOperationException("Unable to create WScript.Shell.");

        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Description = description;
            shortcut.IconLocation = targetPath;
            shortcut.Save();
        }
        finally
        {
            if (shell is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public void DeleteShortcut(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }
}
