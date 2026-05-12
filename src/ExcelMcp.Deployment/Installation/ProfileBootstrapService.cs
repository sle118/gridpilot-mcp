using System.Text.Json;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.Deployment.Installation;

public sealed class ProfileBootstrapService
{
    public string EnsureDefaultProfile(InstalledInstanceState install)
    {
        ArgumentNullException.ThrowIfNull(install);
        var launchDefaults = InstalledHostLaunchDefaultsBuilder.Build(install);

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
                Command = launchDefaults.Command,
                Args = launchDefaults.Args,
                WorkingDirectory = null,
                Env = launchDefaults.Env.ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal)
            },
            Logs = new LaunchProfileLogs
            {
                Path = launchDefaults.RuntimeLogPath,
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
