using System.Text.Json;
using ExcelMcp.Deployment.SmokeTests;

namespace ExcelMcp.IntegrationTests;

public sealed class DeploymentSmokeTests
{
    [RealMcpSmokeFact]
    public async Task RunAsync_BuiltToolHostRespondsToInitializeAndToolsList()
    {
        var hostPath = Path.Combine(AppContext.BaseDirectory, "ExcelMcp.ToolHost.exe");
        Assert.True(File.Exists(hostPath), $"Built host executable was not found at '{hostPath}'.");

        var tempDirectory = Path.Combine(Path.GetTempPath(), "gridpilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var profilePath = Path.Combine(tempDirectory, "profile.json");
            var profileJson = JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    name = "gridpilot-default",
                    displayName = "GridPilot MCP",
                    host = new
                    {
                        command = hostPath,
                        args = new[] { "--session-mode", "create-new", "--log-level", "off" },
                        workingDirectory = AppContext.BaseDirectory,
                        env = new Dictionary<string, string?>()
                    },
                    logs = new
                    {
                        path = (string?)null,
                        stdoutPolicy = "jsonRpcOnly"
                    }
                },
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(profilePath, profileJson);

            var report = await new McpSmokeTestRunner().RunAsync(
                profilePath,
                new McpSmokeTestOptions
                {
                    Timeout = TimeSpan.FromSeconds(10),
                    ShutdownTimeout = TimeSpan.FromSeconds(1)
                });

            Assert.True(report.IsSuccess, string.Join(Environment.NewLine, report.Results.Select(result => $"{result.Id}: {result.Status}: {result.Message}")));
            Assert.Contains(report.Results, result => result.Id == "mcp.initialize" && result.Status == McpSmokeTestStatus.Success);
            Assert.Contains(report.Results, result => result.Id == "mcp.toolsList" && result.Status == McpSmokeTestStatus.Success);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}

internal sealed class RealMcpSmokeFactAttribute : FactAttribute
{
    public RealMcpSmokeFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_GRIDPILOT_REAL_MCP_SMOKE_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Set RUN_GRIDPILOT_REAL_MCP_SMOKE_TESTS=1 to enable real MCP host smoke tests.";
        }
    }
}
