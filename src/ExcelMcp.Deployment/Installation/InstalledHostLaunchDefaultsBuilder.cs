namespace ExcelMcp.Deployment.Installation;

public static class InstalledHostLaunchDefaultsBuilder
{
    public static IReadOnlyList<string> DefaultArgs { get; } =
    [
        "--session-mode",
        "attach",
        "--attach-target",
        "workbook-owner"
    ];

    public static InstalledHostLaunchDefaults Build(InstalledInstanceState install)
    {
        ArgumentNullException.ThrowIfNull(install);

        var runtimeLogPath = Path.Combine(install.Paths.LogRoot, "gridpilot-runtime.log");
        return new InstalledHostLaunchDefaults(
            install.Paths.HostExecutablePath,
            DefaultArgs,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GRIDPILOT_LOG_LEVEL"] = "info",
                ["GRIDPILOT_LOG_PATH"] = runtimeLogPath
            },
            runtimeLogPath);
    }
}
