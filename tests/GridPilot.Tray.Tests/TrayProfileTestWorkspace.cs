using System.Text.Json;

namespace GridPilot.Tray.Tests;

internal sealed class TrayProfileTestWorkspace : IDisposable
{
    private TrayProfileTestWorkspace(string directoryPath, string commandPath)
    {
        DirectoryPath = directoryPath;
        CommandPath = commandPath;
    }

    public string DirectoryPath { get; }

    public string CommandPath { get; }

    public static TrayProfileTestWorkspace Create()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        var commandPath = Path.Combine(directoryPath, "GridPilotHost.exe");
        File.WriteAllText(commandPath, string.Empty);
        return new TrayProfileTestWorkspace(directoryPath, commandPath);
    }

    public string WriteProfile(Dictionary<string, string?>? environment = null, string? workingDirectory = null)
    {
        var profilePath = Path.Combine(DirectoryPath, "profile.json");
        var json = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                name = "gridpilot-default",
                displayName = "GridPilot MCP",
                host = new
                {
                    command = CommandPath,
                    args = new[] { "--session-mode", "attach" },
                    workingDirectory = workingDirectory ?? DirectoryPath,
                    env = environment ?? []
                },
                logs = new
                {
                    path = (string?)null,
                    stdoutPolicy = "jsonRpcOnly"
                },
                metadata = new
                {
                    description = "Test profile"
                }
            });
        File.WriteAllText(profilePath, json);
        return profilePath;
    }

    public string WriteInvalidJsonProfile()
    {
        var profilePath = Path.Combine(DirectoryPath, "invalid-profile.json");
        File.WriteAllText(profilePath, "{");
        return profilePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
