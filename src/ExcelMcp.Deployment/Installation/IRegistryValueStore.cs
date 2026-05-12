namespace ExcelMcp.Deployment.Installation;

public interface IRegistryValueStore
{
    string? GetValue(bool machineWide, string subKey, string name);

    void SetValue(bool machineWide, string subKey, string name, string value);

    void DeleteValue(bool machineWide, string subKey, string name);
}
