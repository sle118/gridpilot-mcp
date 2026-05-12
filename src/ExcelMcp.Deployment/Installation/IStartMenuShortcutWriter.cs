namespace ExcelMcp.Deployment.Installation;

public interface IStartMenuShortcutWriter
{
    void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description);

    void DeleteShortcut(string shortcutPath);
}
