using System.Text.Json;
using Xunit;

namespace GridPilot.Tray.Tests;

public sealed class TrayProfileContextTests
{
    [Fact]
    public void Resolve_UsesProfileArgumentBeforeEnvironment()
    {
        using var env = new EnvironmentVariableScope("GRIDPILOT_PROFILE_PATH", @"C:\env\profile.json");

        var context = TrayProfileContext.Resolve(["--profile", @"C:\cli\profile.json"]);

        Assert.Equal(@"C:\cli\profile.json", context.ProfilePath);
    }

    [Fact]
    public void Resolve_UsesEnvironmentWhenProfileArgumentMissing()
    {
        using var env = new EnvironmentVariableScope("GRIDPILOT_PROFILE_PATH", @"C:\env\profile.json");

        var context = TrayProfileContext.Resolve([]);

        Assert.Equal(@"C:\env\profile.json", context.ProfilePath);
    }

    [Fact]
    public void GetStatus_DisablesProfileActionsWhenNoProfileConfigured()
    {
        using var env = new EnvironmentVariableScope("GRIDPILOT_PROFILE_PATH", null);

        var status = TrayProfileContext.Resolve([]).GetStatus();

        Assert.Equal("No profile configured", status.Message);
        Assert.False(status.CanRunProfileActions);
    }

    [Fact]
    public void GetStatus_EnablesProfileActionsForValidProfile()
    {
        using var temp = TrayProfileTestWorkspace.Create();
        var context = new TrayProfileContext(temp.WriteProfile());

        var status = context.GetStatus();

        Assert.Equal("Profile loaded", status.Message);
        Assert.True(status.CanRunProfileActions);
    }

    private sealed class TrayProfileTestWorkspace : IDisposable
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

        public string WriteProfile()
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
                        args = Array.Empty<string>(),
                        workingDirectory = DirectoryPath,
                        env = new Dictionary<string, string?>()
                    },
                    logs = new
                    {
                        path = (string?)null,
                        stdoutPolicy = "jsonRpcOnly"
                    }
                });
            File.WriteAllText(profilePath, json);
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

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previous);
        }
    }
}
