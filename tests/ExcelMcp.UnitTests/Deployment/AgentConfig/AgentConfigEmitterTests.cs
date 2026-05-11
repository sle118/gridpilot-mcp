using ExcelMcp.Deployment.AgentConfig;
using ExcelMcp.Deployment.Profiles;

namespace ExcelMcp.UnitTests.Deployment.AgentConfig;

public sealed class AgentConfigEmitterTests
{
    [Fact]
    public void Emit_VsCodeCopilot_IncludesCommandArgsEnvAndWarning()
    {
        using var temp = TestProfileWorkspace.Create();

        var snippet = AgentConfigEmitter.Emit(temp.BuildProfile(), AgentTarget.VsCodeCopilot);

        Assert.True(snippet.IsSuccess);
        Assert.Equal("VS Code / GitHub Copilot", snippet.DisplayName);
        Assert.Equal("mcp.json", snippet.SuggestedFileName);
        Assert.Equal("json", snippet.Language);
        Assert.Contains(snippet.Issues, issue =>
            issue.Severity == AgentConfigIssueSeverity.Warning &&
            issue.Code == "vscode_cwd_not_emitted");
        Assert.Equal(
            string.Join("\n",
                "{",
                "  \"servers\": {",
                "    \"gridpilot-default\": {",
                "      \"type\": \"stdio\",",
                $"      \"command\": {JsonString(temp.CommandPath)},",
                "      \"args\": [",
                "        \"--session-mode\",",
                "        \"attach\",",
                "        \"--attach-target\",",
                "        \"workbook-owner\"",
                "      ],",
                "      \"env\": {",
                "        \"GRIDPILOT_LOG_LEVEL\": \"info\"",
                "      }",
                "    }",
                "  }",
                "}"),
            snippet.Content);
    }

    [Fact]
    public void Emit_CodexCli_IncludesCommandArgsCwdEnvAndEnabled()
    {
        using var temp = TestProfileWorkspace.Create();

        var snippet = AgentConfigEmitter.Emit(temp.BuildProfile(), AgentTarget.CodexCli);

        Assert.True(snippet.IsSuccess);
        Assert.Equal("Codex CLI", snippet.DisplayName);
        Assert.Equal("config.toml", snippet.SuggestedFileName);
        Assert.Equal("toml", snippet.Language);
        Assert.Empty(snippet.Issues);
        Assert.Equal(
            string.Join("\n",
                "[mcp_servers.gridpilot-default]",
                $"command = {TomlString(temp.CommandPath)}",
                "args = [\"--session-mode\", \"attach\", \"--attach-target\", \"workbook-owner\"]",
                $"cwd = {TomlString(temp.DirectoryPath)}",
                "enabled = true",
                "",
                "[mcp_servers.gridpilot-default.env]",
                "GRIDPILOT_LOG_LEVEL = \"info\"",
                ""),
            snippet.Content);
    }

    [Fact]
    public void Emit_ClaudeCode_IncludesCommandArgsEnvAndWarning()
    {
        using var temp = TestProfileWorkspace.Create();

        var snippet = AgentConfigEmitter.Emit(temp.BuildProfile(), AgentTarget.ClaudeCode);

        Assert.True(snippet.IsSuccess);
        Assert.Equal("Claude Code", snippet.DisplayName);
        Assert.Equal(".mcp.json", snippet.SuggestedFileName);
        Assert.Equal("json", snippet.Language);
        Assert.Contains(snippet.Issues, issue =>
            issue.Severity == AgentConfigIssueSeverity.Warning &&
            issue.Code == "claude_cwd_not_emitted");
        Assert.Equal(
            string.Join("\n",
                "{",
                "  \"mcpServers\": {",
                "    \"gridpilot-default\": {",
                "      \"type\": \"stdio\",",
                $"      \"command\": {JsonString(temp.CommandPath)},",
                "      \"args\": [",
                "        \"--session-mode\",",
                "        \"attach\",",
                "        \"--attach-target\",",
                "        \"workbook-owner\"",
                "      ],",
                "      \"env\": {",
                "        \"GRIDPILOT_LOG_LEVEL\": \"info\"",
                "      }",
                "    }",
                "  }",
                "}"),
            snippet.Content);
    }

    [Fact]
    public void Emit_GenericMcpJson_IncludesCommandArgsCwdAndEnv()
    {
        using var temp = TestProfileWorkspace.Create();

        var snippet = AgentConfigEmitter.Emit(temp.BuildProfile(), AgentTarget.GenericMcpJson);

        Assert.True(snippet.IsSuccess);
        Assert.Equal("Generic MCP JSON", snippet.DisplayName);
        Assert.Equal("mcp.json", snippet.SuggestedFileName);
        Assert.Equal("json", snippet.Language);
        Assert.Empty(snippet.Issues);
        Assert.Equal(
            string.Join("\n",
                "{",
                "  \"mcpServers\": {",
                "    \"gridpilot-default\": {",
                "      \"type\": \"stdio\",",
                $"      \"command\": {JsonString(temp.CommandPath)},",
                "      \"args\": [",
                "        \"--session-mode\",",
                "        \"attach\",",
                "        \"--attach-target\",",
                "        \"workbook-owner\"",
                "      ],",
                "      \"env\": {",
                "        \"GRIDPILOT_LOG_LEVEL\": \"info\"",
                "      },",
                $"      \"cwd\": {JsonString(temp.DirectoryPath)}",
                "    }",
                "  }",
                "}"),
            snippet.Content);
    }

    [Fact]
    public void Emit_EmptyEnv_EmitsEmptyObjectAndTable()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile(env: new Dictionary<string, string?>());

        var jsonSnippet = AgentConfigEmitter.Emit(profile, AgentTarget.GenericMcpJson);
        var tomlSnippet = AgentConfigEmitter.Emit(profile, AgentTarget.CodexCli);

        Assert.Contains("\"env\": {}", jsonSnippet.Content, StringComparison.Ordinal);
        Assert.EndsWith("[mcp_servers.gridpilot-default.env]\n", tomlSnippet.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Emit_NullEnv_ReturnsValidationErrorsAndNoContent()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfileWithNullEnv();

        var snippet = AgentConfigEmitter.Emit(profile, AgentTarget.GenericMcpJson);

        Assert.False(snippet.IsSuccess);
        Assert.Equal(string.Empty, snippet.Content);
        Assert.Contains(snippet.Issues, issue =>
            issue.Severity == AgentConfigIssueSeverity.Error &&
            issue.Code == "host_env_required");
    }

    [Fact]
    public void Emit_InvalidProfile_ReturnsValidationErrorsAndNoContent()
    {
        using var temp = TestProfileWorkspace.Create();
        var profile = temp.BuildProfile(commandPath: Path.Combine(temp.DirectoryPath, "missing.exe"));

        var snippet = AgentConfigEmitter.Emit(profile, AgentTarget.CodexCli);

        Assert.False(snippet.IsSuccess);
        Assert.Equal("Codex CLI", snippet.DisplayName);
        Assert.Equal(string.Empty, snippet.Content);
        Assert.Contains(snippet.Issues, issue =>
            issue.Severity == AgentConfigIssueSeverity.Error &&
            issue.Code == "host_command_not_found");
    }

    private static string JsonString(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"";

    private static string TomlString(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"";

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

        public LaunchProfile BuildProfile(
            string? commandPath = null,
            IReadOnlyDictionary<string, string?>? env = null) =>
            new()
            {
                SchemaVersion = 1,
                Name = "gridpilot-default",
                DisplayName = "GridPilot MCP",
                Host = new LaunchProfileHost
                {
                    Command = commandPath ?? CommandPath,
                    Args = ["--session-mode", "attach", "--attach-target", "workbook-owner"],
                    WorkingDirectory = DirectoryPath,
                    Env = env ?? new Dictionary<string, string?> { ["GRIDPILOT_LOG_LEVEL"] = "info" }
                },
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

        public LaunchProfile BuildProfileWithNullEnv() =>
            BuildProfile() with
            {
                Host = BuildProfile().Host! with { Env = null }
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
