using ExcelMcp.ToolHost;

namespace ExcelMcp.IntegrationTests;

public sealed class HostOptionsTests
{
    [Fact]
    public void Parse_DefaultsToHiddenCreateNewMode()
    {
        using var _ = new EnvironmentVariableScope("GRIDPILOT_SESSION_MODE", null, "GRIDPILOT_SESSION_VISIBLE", null);

        var options = HostOptions.Parse(Array.Empty<string>());

        Assert.Equal(SessionMode.CreateNew, options.SessionMode);
        Assert.False(options.Visible);
    }

    [Fact]
    public void Parse_AllowsArgsToOverrideEnvironment()
    {
        using var _ = new EnvironmentVariableScope("GRIDPILOT_SESSION_MODE", "attach", "GRIDPILOT_SESSION_VISIBLE", null);

        var options = HostOptions.Parse(["--session-mode", "create-new", "--visible"]);

        Assert.Equal(SessionMode.CreateNew, options.SessionMode);
        Assert.True(options.Visible);
    }

    [Fact]
    public void Parse_ThrowsForInvalidSessionMode()
    {
        using var _ = new EnvironmentVariableScope("GRIDPILOT_SESSION_MODE", null, "GRIDPILOT_SESSION_VISIBLE", null);

        var exception = Assert.Throws<InvalidOperationException>(() => HostOptions.Parse(["--session-mode", "bad-mode"]));

        Assert.Contains("Unsupported session mode", exception.Message, StringComparison.Ordinal);
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
