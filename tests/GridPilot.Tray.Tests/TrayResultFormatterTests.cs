using ExcelMcp.Deployment.Doctor;
using ExcelMcp.Deployment.SmokeTests;
using Xunit;

namespace GridPilot.Tray.Tests;

public sealed class TrayResultFormatterTests
{
    [Fact]
    public void FormatDoctor_IncludesSeverityNameMessageAndNextStep()
    {
        var report = new DoctorReport(
        [
            new DoctorCheckResult("profile", "Profile", DoctorCheckSeverity.Error, "Missing profile.", "Choose a profile.")
        ]);

        var text = TrayResultFormatter.FormatDoctor(report);

        Assert.Contains("GridPilot Doctor", text, StringComparison.Ordinal);
        Assert.Contains("[Error] Profile: Missing profile.", text, StringComparison.Ordinal);
        Assert.Contains("Next: Choose a profile.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSmoke_IncludesMissingToolsAndStderrTail()
    {
        var report = new McpSmokeTestReport(
            [
                new McpSmokeTestStepResult("tools", "Tools", McpSmokeTestStatus.Failure, "Missing tools.", "Rebuild host.")
            ],
            McpSmokeTestTransportMode.Framed,
            ExitCode: null,
            WasKilled: false,
            StderrTail: "stderr text",
            MissingToolNames: ["range_read"]);

        var text = TrayResultFormatter.FormatSmoke(report);

        Assert.Contains("GridPilot MCP Smoke Test", text, StringComparison.Ordinal);
        Assert.Contains("Overall result: Attention needed", text, StringComparison.Ordinal);
        Assert.Contains("Transport: Framed", text, StringComparison.Ordinal);
        Assert.Contains("[Failure] Tools: Missing tools.", text, StringComparison.Ordinal);
        Assert.Contains("Missing tools: range_read", text, StringComparison.Ordinal);
        Assert.Contains("stderr text", text, StringComparison.Ordinal);
    }
}
