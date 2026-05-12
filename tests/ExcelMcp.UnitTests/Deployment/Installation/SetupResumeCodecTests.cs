using ExcelMcp.Deployment.Installation;

namespace ExcelMcp.UnitTests.Deployment.Installation;

public sealed class SetupResumeCodecTests
{
    [Fact]
    public void EncodeDecode_RoundTripsResumeState()
    {
        var state = new SetupResumeState(
            SetupOperationKind.Update,
            new SetupOptions(InstallScope.MachineWide, @"C:\downloads\gridpilot", StartupEnabled: true, CreateStartMenuShortcut: false));

        var encoded = SetupResumeCodec.Encode(state);
        var decoded = SetupResumeCodec.Decode(encoded);

        Assert.Equal(state, decoded);
    }
}
