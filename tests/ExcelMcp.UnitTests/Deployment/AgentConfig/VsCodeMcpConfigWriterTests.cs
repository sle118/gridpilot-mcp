using System.Text.Json.Nodes;
using ExcelMcp.Deployment.AgentConfig;
using ExcelMcp.Deployment.Installation;
using ExcelMcp.UnitTests.Deployment.Installation;

namespace ExcelMcp.UnitTests.Deployment.AgentConfig;

public sealed class VsCodeMcpConfigWriterTests
{
    [Fact]
    public void WriteForInstalledInstance_CreatesNewFileAtOverridePath()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        var configPath = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"), "mcp.json");

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, configPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(VsCodeMcpConfigWriteAction.Create, result.Action);
        Assert.True(result.WasWritten);
        Assert.Equal(configPath, result.ConfigPath);
        Assert.Null(result.BackupPath);
        Assert.Contains("+    \"gridpilot\": {", result.Diff, StringComparison.Ordinal);
        Assert.True(File.Exists(configPath));

        var root = ReadJsonObject(configPath);
        Assert.Equal("stdio", root["servers"]?["gridpilot"]?["type"]?.GetValue<string>());
        Assert.Equal(install.Paths.HostExecutablePath, root["servers"]?["gridpilot"]?["command"]?.GetValue<string>());
        Assert.Equal(
            Path.Combine(install.Paths.LogRoot, "gridpilot-runtime.log"),
            root["servers"]?["gridpilot"]?["env"]?["GRIDPILOT_LOG_PATH"]?.GetValue<string>());

        Directory.Delete(Path.GetDirectoryName(configPath)!, recursive: true);
    }

    [Fact]
    public void WriteForInstalledInstance_PreservesUnrelatedTopLevelContent()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create(
            """
            {
              "inputs": [
                {
                  "type": "promptString",
                  "id": "sample"
                }
              ]
            }
            """);

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.True(result.IsSuccess);
        var root = ReadJsonObject(file.Path);
        Assert.Equal("promptString", root["inputs"]?[0]?["type"]?.GetValue<string>());
        Assert.NotNull(root["servers"]?["gridpilot"]);
    }

    [Fact]
    public void WriteForInstalledInstance_PreservesUnrelatedServers()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create(
            """
            {
              "servers": {
                "other": {
                  "type": "stdio",
                  "command": "other.exe"
                }
              }
            }
            """);

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.True(result.IsSuccess);
        var root = ReadJsonObject(file.Path);
        Assert.Equal("other.exe", root["servers"]?["other"]?["command"]?.GetValue<string>());
        Assert.Equal(install.Paths.HostExecutablePath, root["servers"]?["gridpilot"]?["command"]?.GetValue<string>());
    }

    [Fact]
    public void WriteForInstalledInstance_ReplacesExistingGridPilotServer()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create(
            """
            {
              "servers": {
                "gridpilot": {
                  "type": "stdio",
                  "command": "C:\\legacy\\gridpilot.exe",
                  "args": []
                }
              }
            }
            """);

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.True(result.IsSuccess);
        Assert.Equal(VsCodeMcpConfigWriteAction.Update, result.Action);
        Assert.Contains("-      \"command\": \"C:\\\\legacy\\\\gridpilot.exe\"", result.Diff, StringComparison.Ordinal);
        Assert.Contains($"+      \"command\": {JsonString(install.Paths.HostExecutablePath)}", result.Diff, StringComparison.Ordinal);

        var root = ReadJsonObject(file.Path);
        Assert.Equal(install.Paths.HostExecutablePath, root["servers"]?["gridpilot"]?["command"]?.GetValue<string>());
    }

    [Fact]
    public void WriteForInstalledInstance_ReturnsFailureForMalformedJson()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create("{");

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.False(result.IsSuccess);
        Assert.Equal(VsCodeMcpConfigWriteAction.Failed, result.Action);
        Assert.False(result.WasWritten);
        Assert.Null(result.BackupPath);
        Assert.Contains(result.Issues, issue => issue.Code == "vscode_mcp_json_invalid");
        Assert.Equal("{", File.ReadAllText(file.Path));
    }

    [Fact]
    public void WriteForInstalledInstance_ReturnsFailureForNonObjectRoot()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create("[]");

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "vscode_mcp_root_not_object");
        Assert.Equal("[]", File.ReadAllText(file.Path));
    }

    [Fact]
    public void WriteForInstalledInstance_ReturnsFailureForNonObjectServers()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create(
            """
            {
              "servers": 5
            }
            """);

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == "vscode_mcp_servers_not_object");
        Assert.Equal(
            """
            {
              "servers": 5
            }
            """,
            File.ReadAllText(file.Path).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteForInstalledInstance_CreatesBackupBeforeRealWrite()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create(
            """
            {
              "servers": {
                "gridpilot": {
                  "type": "stdio",
                  "command": "old.exe"
                }
              }
            }
            """);
        var original = File.ReadAllText(file.Path);

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(original, File.ReadAllText(result.BackupPath!));
    }

    [Fact]
    public void WriteForInstalledInstance_DryRunDoesNotWriteOrBackup()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        using var file = TempFile.Create(
            """
            {
              "servers": {
                "other": {
                  "type": "stdio",
                  "command": "other.exe"
                }
              }
            }
            """);
        var original = File.ReadAllText(file.Path);

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path, dryRun: true);

        Assert.True(result.IsSuccess);
        Assert.False(result.WasWritten);
        Assert.Null(result.BackupPath);
        Assert.Contains("Dry run only; no files were written.", result.SummaryLines);
        Assert.NotEmpty(result.Diff);
        Assert.Equal(original, File.ReadAllText(file.Path));
    }

    [Fact]
    public void WriteForInstalledInstance_ReturnsNoChangeWhenConfigAlreadyMatchesSemantically()
    {
        using var workspace = InstallationTestWorkspace.Create();
        var install = workspace.CreateInstalledInstanceState(InstallScope.PerUser, "v1.2.3");
        var expectedLogPath = Path.Combine(install.Paths.LogRoot, "gridpilot-runtime.log");
        using var file = TempFile.Create(
            "{"
            + "\"servers\":{"
            + "\"gridpilot\":{"
            + "\"type\":\"stdio\","
            + $"\"command\":{JsonString(install.Paths.HostExecutablePath)},"
            + "\"args\":[\"--session-mode\",\"attach\",\"--attach-target\",\"workbook-owner\"],"
            + "\"env\":{"
            + "\"GRIDPILOT_LOG_LEVEL\":\"info\","
            + $"\"GRIDPILOT_LOG_PATH\":{JsonString(expectedLogPath)}"
            + "}"
            + "}"
            + "}"
            + "}");
        var original = File.ReadAllText(file.Path);

        var result = new VsCodeMcpConfigWriter().WriteForInstalledInstance(install, file.Path);

        Assert.True(result.IsSuccess);
        Assert.Equal(VsCodeMcpConfigWriteAction.NoChange, result.Action);
        Assert.False(result.WasWritten);
        Assert.Null(result.BackupPath);
        Assert.Equal(string.Empty, result.Diff);
        Assert.Equal(original, File.ReadAllText(file.Path));
    }

    private static JsonObject ReadJsonObject(string path) =>
        JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static string JsonString(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"";

    private sealed class TempFile : IDisposable
    {
        private TempFile(string directoryPath, string path)
        {
            DirectoryPath = directoryPath;
            Path = path;
        }

        public string DirectoryPath { get; }

        public string Path { get; }

        public static TempFile Create(string content)
        {
            var directoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var path = System.IO.Path.Combine(directoryPath, "mcp.json");
            File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal));
            return new TempFile(directoryPath, path);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
