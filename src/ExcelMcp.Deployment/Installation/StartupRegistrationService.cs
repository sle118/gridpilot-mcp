namespace ExcelMcp.Deployment.Installation;

public sealed class StartupRegistrationService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GridPilot MCP";
    private readonly IRegistryValueStore _registryValueStore;

    public StartupRegistrationService(IRegistryValueStore? registryValueStore = null)
    {
        _registryValueStore = registryValueStore ?? new WindowsRegistryValueStore();
    }

    public string BuildCommand(StartupRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var arguments = options.Arguments.Count == 0
            ? string.Empty
            : $" {string.Join(" ", options.Arguments.Select(QuoteArgument))}";
        return $"\"{options.TrayExecutablePath}\"{arguments}";
    }

    public bool IsEnabled(StartupRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var current = _registryValueStore.GetValue(
            options.Scope == InstallScope.MachineWide,
            RunSubKey,
            ValueName);
        return string.Equals(current, BuildCommand(options), StringComparison.OrdinalIgnoreCase);
    }

    public void Enable(StartupRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _registryValueStore.SetValue(
            options.Scope == InstallScope.MachineWide,
            RunSubKey,
            ValueName,
            BuildCommand(options));
    }

    public void Disable(InstallScope scope)
    {
        _registryValueStore.DeleteValue(
            scope == InstallScope.MachineWide,
            RunSubKey,
            ValueName);
    }

    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        return argument.IndexOfAny([' ', '\t', '"']) >= 0
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
    }
}
