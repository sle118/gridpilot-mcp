using ExcelMcp.ToolHost;
using ExcelMcp.Core;
using ExcelMcp.Core.Logging;

namespace ExcelMcp.IntegrationTests;

public sealed class HostOptionsTests
{
    [Fact]
    public void Parse_DefaultsToHiddenCreateNewMode()
    {
        using var _ = new EnvironmentVariableScope("GRIDPILOT_SESSION_MODE", null, "GRIDPILOT_SESSION_VISIBLE", null, "GRIDPILOT_LOG_LEVEL", null, "GRIDPILOT_LOG_PATH", null);

        var options = HostOptions.Parse(Array.Empty<string>());

        Assert.Equal(SessionMode.CreateNew, options.SessionMode);
        Assert.Equal(SessionAttachTargetMode.WorkbookOwner, options.AttachTarget);
        Assert.False(options.Visible);
        Assert.Equal(GridPilotLogLevel.Off, options.LogLevel);
        Assert.Null(options.LogPath);
    }

    [Fact]
    public void Parse_AllowsArgsToOverrideEnvironment()
    {
        using var _ = new EnvironmentVariableScope(
            "GRIDPILOT_SESSION_MODE", "attach",
            "GRIDPILOT_SESSION_VISIBLE", null,
            "GRIDPILOT_LOG_LEVEL", "debug",
            "GRIDPILOT_LOG_PATH", @"C:\temp\env.log");

        var options = HostOptions.Parse([
            "--session-mode", "create-new",
            "--attach-target", "any-running",
            "--visible",
            "--log-level", "info",
            "--log-path", @"C:\temp\args.log"]);

        Assert.Equal(SessionMode.CreateNew, options.SessionMode);
        Assert.Equal(SessionAttachTargetMode.AnyRunningInstance, options.AttachTarget);
        Assert.True(options.Visible);
        Assert.Equal(GridPilotLogLevel.Info, options.LogLevel);
        Assert.Equal(@"C:\temp\args.log", options.LogPath);
    }

    [Fact]
    public void Parse_ThrowsForInvalidSessionMode()
    {
        using var _ = new EnvironmentVariableScope("GRIDPILOT_SESSION_MODE", null, "GRIDPILOT_SESSION_VISIBLE", null);

        var exception = Assert.Throws<InvalidOperationException>(() => HostOptions.Parse(["--session-mode", "bad-mode"]));

        Assert.Contains("Unsupported session mode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ThrowsForInvalidAttachTarget()
    {
        using var _ = new EnvironmentVariableScope("GRIDPILOT_SESSION_MODE", null, "GRIDPILOT_SESSION_VISIBLE", null, "GRIDPILOT_ATTACH_TARGET", null);

        var exception = Assert.Throws<InvalidOperationException>(() => HostOptions.Parse(["--attach-target", "bad-target"]));

        Assert.Contains("Unsupported attach target", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ThrowsForInvalidLogLevel()
    {
        using var _ = new EnvironmentVariableScope("GRIDPILOT_LOG_LEVEL", null, "GRIDPILOT_LOG_PATH", null);

        var exception = Assert.Throws<InvalidOperationException>(() => HostOptions.Parse(["--log-level", "loud"]));

        Assert.Contains("Unsupported log level", exception.Message, StringComparison.Ordinal);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly (string Name, string? Value)[] _originals;

        public EnvironmentVariableScope(params string?[] pairs)
        {
            _originals = new (string Name, string? Value)[pairs.Length / 2];
            for (var index = 0; index < pairs.Length; index += 2)
            {
                var name = pairs[index]!;
                var value = pairs[index + 1];
                _originals[index / 2] = (name, Environment.GetEnvironmentVariable(name));
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originals)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
