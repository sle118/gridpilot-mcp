using System.Text.Json;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.Deployment.Installation;

public sealed class ProfileBootstrapService
{
    public string EnsureDefaultProfile(InstalledInstanceState install)
    {
        ArgumentNullException.ThrowIfNull(install);

        Directory.CreateDirectory(install.Paths.ProfileRoot);
        Directory.CreateDirectory(install.Paths.LogRoot);

        if (File.Exists(install.Paths.DefaultProfilePath))
        {
            return install.Paths.DefaultProfilePath;
        }

        var profile = new LaunchProfile
        {
            SchemaVersion = 1,
            Name = "gridpilot-default",
            DisplayName = "GridPilot MCP",
            Host = new LaunchProfileHost
            {
                Command = install.Paths.HostExecutablePath,
                Args = ["--session-mode", "attach", "--attach-target", "workbook-owner"],
                WorkingDirectory = Path.GetDirectoryName(install.Paths.HostExecutablePath),
                Env = new Dictionary<string, string?>
                {
                    ["GRIDPILOT_LOG_LEVEL"] = "info",
                    ["GRIDPILOT_LOG_PATH"] = Path.Combine(install.Paths.LogRoot, "gridpilot-runtime.log")
                }
            },
            Logs = new LaunchProfileLogs
            {
                Path = Path.Combine(install.Paths.LogRoot, "gridpilot-runtime.log"),
                StdoutPolicy = "jsonRpcOnly"
            },
            Metadata = new LaunchProfileMetadata
            {
                Description = "Default installed GridPilot MCP launch profile"
            }
        };

        File.WriteAllText(
            install.Paths.DefaultProfilePath,
            JsonSerializer.Serialize(profile, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

        return install.Paths.DefaultProfilePath;
    }
}
