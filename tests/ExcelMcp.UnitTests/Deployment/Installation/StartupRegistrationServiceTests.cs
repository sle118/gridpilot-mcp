using ExcelMcp.Deployment.Installation;

namespace ExcelMcp.UnitTests.Deployment.Installation;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void BuildCommand_QuotesExecutableAndArguments()
    {
        var sut = new StartupRegistrationService(new RecordingRegistryValueStore());

        var command = sut.BuildCommand(new StartupRegistrationOptions(
            InstallScope.PerUser,
            @"C:\Program Files\GridPilot MCP\GridPilot.Tray.exe",
            ["--startup", "--profile", @"C:\Users\sle11\AppData\Local\GridPilot MCP\profiles\gridpilot-default.json"]));

        Assert.Equal(
            "\"C:\\Program Files\\GridPilot MCP\\GridPilot.Tray.exe\" --startup --profile \"C:\\Users\\sle11\\AppData\\Local\\GridPilot MCP\\profiles\\gridpilot-default.json\"",
            command);
    }

    [Fact]
    public void Enable_UsesCurrentUserHiveForPerUserScope()
    {
        var registry = new RecordingRegistryValueStore();
        var sut = new StartupRegistrationService(registry);

        sut.Enable(new StartupRegistrationOptions(InstallScope.PerUser, @"C:\temp\GridPilot.Tray.exe", ["--startup", "--no-dashboard"]));

        Assert.False(registry.LastWriteMachineWide);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", registry.LastWriteSubKey);
        Assert.Equal("GridPilot MCP", registry.LastWriteName);
    }

    [Fact]
    public void Enable_UsesLocalMachineHiveForMachineWideScope()
    {
        var registry = new RecordingRegistryValueStore();
        var sut = new StartupRegistrationService(registry);

        sut.Enable(new StartupRegistrationOptions(InstallScope.MachineWide, @"C:\Program Files\GridPilot MCP\GridPilot.Tray.exe", ["--startup", "--no-dashboard"]));

        Assert.True(registry.LastWriteMachineWide);
    }

    private sealed class RecordingRegistryValueStore : IRegistryValueStore
    {
        private readonly Dictionary<(bool MachineWide, string SubKey, string Name), string> _values = [];

        public bool LastWriteMachineWide { get; private set; }

        public string? LastWriteSubKey { get; private set; }

        public string? LastWriteName { get; private set; }

        public string? GetValue(bool machineWide, string subKey, string name) =>
            _values.TryGetValue((machineWide, subKey, name), out var value) ? value : null;

        public void SetValue(bool machineWide, string subKey, string name, string value)
        {
            LastWriteMachineWide = machineWide;
            LastWriteSubKey = subKey;
            LastWriteName = name;
            _values[(machineWide, subKey, name)] = value;
        }

        public void DeleteValue(bool machineWide, string subKey, string name) =>
            _values.Remove((machineWide, subKey, name));
    }
}
