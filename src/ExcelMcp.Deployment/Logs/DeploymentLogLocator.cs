using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.Deployment.Logs;

public static class DeploymentLogLocator
{
    public const string RuntimeLogEnvironmentVariable = "GRIDPILOT_LOG_PATH";
    public const string ConventionalRuntimeLogFileName = "gridpilot-runtime.log";
    public const string ConventionalProxyLogDirectoryName = "mcp-proxy";

    public static IReadOnlyList<DeploymentLogEntry> Locate(LaunchProfile profile, string? currentDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var candidates = new List<(DeploymentLogKind Kind, string Path)>();
        AddIfPresent(candidates, DeploymentLogKind.ProfileConfigured, profile.Logs?.Path);

        if (TryGetEnvironmentLogPath(profile, out var environmentLogPath))
        {
            AddIfPresent(candidates, DeploymentLogKind.HostEnvironment, environmentLogPath);
        }

        var baseDirectory = ResolveBaseDirectory(profile, currentDirectory);
        candidates.Add((DeploymentLogKind.HostConventional, Path.Combine(baseDirectory, ".tmp", ConventionalRuntimeLogFileName)));
        candidates.Add((DeploymentLogKind.ProxyConventional, Path.Combine(
            baseDirectory,
            ".tmp",
            ConventionalProxyLogDirectoryName,
            $"{ResolveProfileName(profile)}.log")));

        return candidates
            .Select(candidate => Inspect(candidate.Kind, candidate.Path))
            .ToArray();
    }

    private static void AddIfPresent(List<(DeploymentLogKind Kind, string Path)> candidates, DeploymentLogKind kind, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.Add((kind, path));
        }
    }

    private static bool TryGetEnvironmentLogPath(LaunchProfile profile, out string? logPath)
    {
        logPath = null;
        if (profile.Host?.Env is null)
        {
            return false;
        }

        foreach (var (key, value) in profile.Host.Env)
        {
            if (string.Equals(key, RuntimeLogEnvironmentVariable, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(value))
            {
                logPath = value;
                return true;
            }
        }

        return false;
    }

    private static string ResolveBaseDirectory(LaunchProfile profile, string? currentDirectory)
    {
        if (!string.IsNullOrWhiteSpace(profile.Host?.WorkingDirectory))
        {
            return profile.Host.WorkingDirectory;
        }

        return string.IsNullOrWhiteSpace(currentDirectory)
            ? Environment.CurrentDirectory
            : currentDirectory;
    }

    private static string ResolveProfileName(LaunchProfile profile) =>
        string.IsNullOrWhiteSpace(profile.Name) ? "gridpilot" : profile.Name;

    private static DeploymentLogEntry Inspect(DeploymentLogKind kind, string path)
    {
        if (!File.Exists(path))
        {
            return new DeploymentLogEntry(
                kind,
                path,
                Exists: false,
                SizeBytes: null,
                LastWriteTimeUtc: null,
                DeploymentLogAccessStatus.Missing,
                "Log file does not exist.");
        }

        try
        {
            var fileInfo = new FileInfo(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new DeploymentLogEntry(
                kind,
                path,
                Exists: true,
                SizeBytes: fileInfo.Length,
                LastWriteTimeUtc: fileInfo.LastWriteTimeUtc,
                DeploymentLogAccessStatus.Accessible);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new DeploymentLogEntry(
                kind,
                path,
                Exists: true,
                SizeBytes: null,
                LastWriteTimeUtc: null,
                DeploymentLogAccessStatus.Unreadable,
                exception.Message);
        }
    }
}
