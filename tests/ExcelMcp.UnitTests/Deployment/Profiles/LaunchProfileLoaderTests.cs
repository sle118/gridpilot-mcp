using ExcelMcp.Deployment.Profiles;
using System.Text.Json;

namespace ExcelMcp.UnitTests.Deployment.Profiles;

public sealed class LaunchProfileLoaderTests
{
    [Fact]
    public void Load_ValidProfile_ReturnsProfile()
    {
        using var temp = TestProfileWorkspace.Create();
        var profilePath = temp.WriteProfile(TestProfileWorkspace.BuildProfileJson(temp.CommandPath, temp.DirectoryPath));

        var result = LaunchProfileLoader.Load(profilePath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Profile);
        Assert.Empty(result.Issues);
        Assert.Equal(1, result.Profile.SchemaVersion);
        Assert.Equal("gridpilot-default", result.Profile.Name);
        Assert.Equal(temp.CommandPath, result.Profile.Host?.Command);
    }

    [Fact]
    public void Load_MissingProfileFile_ReturnsIssue()
    {
        var result = LaunchProfileLoader.Load(Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"), "missing.json"));

        var issue = Assert.Single(result.Issues);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Profile);
        Assert.Equal(LaunchProfileIssueSeverity.Error, issue.Severity);
        Assert.Equal("profile_not_found", issue.Code);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profilePath = temp.WriteProfile("{");

        var result = LaunchProfileLoader.Load(profilePath);

        var issue = Assert.Single(result.Issues);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Profile);
        Assert.Equal("profile_invalid_json", issue.Code);
    }

    [Fact]
    public void Load_MalformedArgsShape_ReturnsDeserializationIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profilePath = temp.WriteProfile(TestProfileWorkspace.BuildProfileJson(temp.CommandPath, temp.DirectoryPath, argsJson: "\"--session-mode attach\""));

        var result = LaunchProfileLoader.Load(profilePath);

        var issue = Assert.Single(result.Issues);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Profile);
        Assert.Equal("profile_deserialization_failed", issue.Code);
    }

    [Fact]
    public void Load_MalformedEnvShape_ReturnsDeserializationIssue()
    {
        using var temp = TestProfileWorkspace.Create();
        var profilePath = temp.WriteProfile(TestProfileWorkspace.BuildProfileJson(temp.CommandPath, temp.DirectoryPath, envJson: "[]"));

        var result = LaunchProfileLoader.Load(profilePath);

        var issue = Assert.Single(result.Issues);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Profile);
        Assert.Equal("profile_deserialization_failed", issue.Code);
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

        public string WriteProfile(string json)
        {
            var profilePath = Path.Combine(DirectoryPath, "profile.json");
            File.WriteAllText(profilePath, json);
            return profilePath;
        }

        public static string BuildProfileJson(
            string commandPath,
            string workingDirectory,
            string argsJson = "[\"--session-mode\", \"attach\", \"--attach-target\", \"workbook-owner\"]",
            string envJson = "{\"GRIDPILOT_LOG_LEVEL\":\"info\"}",
            string stdoutPolicy = "jsonRpcOnly") =>
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                name = "gridpilot-default",
                displayName = "GridPilot MCP",
                host = new
                {
                    command = commandPath,
                    args = JsonDocument.Parse(argsJson).RootElement,
                    workingDirectory,
                    env = JsonDocument.Parse(envJson).RootElement
                },
                logs = new
                {
                    path = (string?)null,
                    stdoutPolicy
                },
                metadata = new
                {
                    description = "Default local GridPilot MCP launch profile"
                }
            });

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}

