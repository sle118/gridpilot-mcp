namespace ExcelMcp.Deployment.Installation;

public sealed record InstalledHostLaunchDefaults(
    string Command,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env,
    string RuntimeLogPath);
