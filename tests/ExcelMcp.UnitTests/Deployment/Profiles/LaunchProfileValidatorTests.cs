using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.UnitTests.Deployment.Profiles;

public sealed class LaunchProfileValidatorTests
{
    [Fact]
    public void Validate_ValidProfile_IsValid()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile();

        var result = LaunchProfileValidator.Validate(profile);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with { SchemaVersion = 2 };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "unsupported_schema_version");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingCommand_ReturnsIssue(string? command)
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Host = temp.BuildHost() with { Command = command }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "host_command_required");
    }

    [Fact]
    public void Validate_NonexistentCommand_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Host = temp.BuildHost() with { Command = Path.Combine(temp.DirectoryPath, "missing.exe") }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "host_command_not_found");
    }

    [Fact]
    public void Validate_NonexistentWorkingDirectory_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Host = temp.BuildHost() with { WorkingDirectory = Path.Combine(temp.DirectoryPath, "missing") }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "host_working_directory_not_found");
    }

    [Fact]
    public void Validate_NullArgs_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Host = temp.BuildHost() with { Args = null }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "host_args_required");
    }

    [Fact]
    public void Validate_NullEnv_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Host = temp.BuildHost() with { Env = null }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "host_env_required");
    }

    [Fact]
    public void Validate_EmptyEnvKey_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Host = temp.BuildHost() with
            {
                Env = new Dictionary<string, string?> { [""] = "value" }
            }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "host_env_key_required");
    }

    [Fact]
    public void Validate_NullEnvValue_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Host = temp.BuildHost() with
            {
                Env = new Dictionary<string, string?> { ["GRIDPILOT_LOG_LEVEL"] = null }
            }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "host_env_value_required");
    }

    [Fact]
    public void Validate_UnsupportedStdoutPolicy_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile() with
        {
            Logs = new LaunchProfileLogs
            {
                Path = null,
                StdoutPolicy = "diagnosticTextAllowed"
            }
        };

        var result = LaunchProfileValidator.Validate(profile);

        AssertContains(result, "unsupported_stdout_policy");
    }

    [Fact]
    public void Validate_DoesNotRequireMutationPolicy()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile();

        var result = LaunchProfileValidator.Validate(profile);

        Assert.True(result.IsValid);
    }

    private static void AssertContains(LaunchProfileValidationResult result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Severity == LaunchProfileIssueSeverity.Error &&
            string.Equals(issue.Code, code, StringComparison.Ordinal));
    }

    private sealed class TestProfileWorkspace : IDisposable
    {
        private TestProfileWorkspace(string directoryPath, string commandPath)
        {
            DirectoryPath = directoryPath;
            CommandPath = commandPath;
        }

        public string DirectoryPath { get; }

        public string CommandPath { get; }

        public static TestProfileWorkspace Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var commandPath = Path.Combine(directoryPath, "ExcelMcp.ToolHost.exe");
            File.WriteAllText(commandPath, string.Empty);
            return new TestProfileWorkspace(directoryPath, commandPath);
        }

        public LaunchProfile BuildProfile() =>
            new()
            {
                SchemaVersion = 1,
                Name = "gridpilot-default",
                DisplayName = "GridPilot MCP",
                Host = BuildHost(),
                Logs = new LaunchProfileLogs
                {
                    Path = null,
                    StdoutPolicy = LaunchProfileValidator.JsonRpcOnlyStdoutPolicy
                },
                Metadata = new LaunchProfileMetadata
                {
                    Description = "Default local GridPilot MCP launch profile"
                }
            };

        public LaunchProfileHost BuildHost() =>
            new()
            {
                Command = CommandPath,
                Args = ["--session-mode", "attach", "--attach-target", "workbook-owner"],
                WorkingDirectory = DirectoryPath,
                Env = new Dictionary<string, string?> { ["GRIDPILOT_LOG_LEVEL"] = "info" }
            };

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}

