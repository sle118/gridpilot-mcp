using Microsoft.Win32;

namespace ExcelMcp.Deployment.Installation;

public sealed class WindowsRegistryValueStore : IRegistryValueStore
{
    public string? GetValue(bool machineWide, string subKey, string name)
    {
        using var key = OpenOrCreate(machineWide, subKey, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetValue(bool machineWide, string subKey, string name, string value)
    {
        using var key = OpenOrCreate(machineWide, subKey, writable: true) ??
            throw new InvalidOperationException($"Unable to open registry key '{subKey}'.");
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(bool machineWide, string subKey, string name)
    {
        using var key = OpenOrCreate(machineWide, subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    private static RegistryKey? OpenOrCreate(bool machineWide, string subKey, bool writable)
    {
        var hive = machineWide ? Registry.LocalMachine : Registry.CurrentUser;
        return writable
            ? hive.CreateSubKey(subKey, writable: true)
            : hive.OpenSubKey(subKey, writable: false);
    }
}
