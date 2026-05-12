using System.Text;
using ExcelMcp.Deployment.Doctor;
using ExcelMcp.Deployment.SmokeTests;

namespace GridPilot.Tray;

internal static class TrayResultFormatter
{
    public static string FormatDoctor(DoctorReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("GridPilot Doctor");
        builder.AppendLine();
        foreach (var result in report.Results)
        {
            builder.Append('[').Append(result.Severity).Append("] ");
            builder.Append(result.Name).Append(": ").AppendLine(result.Message);
            if (!string.IsNullOrWhiteSpace(result.SuggestedNextStep) &&
                !string.Equals(result.SuggestedNextStep, "No action needed.", StringComparison.Ordinal))
            {
                builder.Append("Next: ").AppendLine(result.SuggestedNextStep);
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatSmoke(McpSmokeTestReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("GridPilot MCP Smoke Test");
        builder.AppendLine();
        builder.Append("Overall result: ")
            .AppendLine(report.IsSuccess ? "Success" : "Attention needed");
        if (report.DetectedTransportMode is not null)
        {
            builder.Append("Transport: ")
                .AppendLine(report.DetectedTransportMode.Value.ToString());
        }

        if (report.ExitCode is not null)
        {
            builder.Append("Exit code: ")
                .AppendLine(report.ExitCode.Value.ToString());
        }

        if (report.WasKilled)
        {
            builder.AppendLine("Cleanup: host process was terminated by the smoke test.");
        }

        builder.AppendLine();
        foreach (var result in report.Results)
        {
            builder.Append('[').Append(result.Status).Append("] ");
            builder.Append(result.Name).Append(": ").AppendLine(result.Message);
            if (!string.IsNullOrWhiteSpace(result.SuggestedNextStep) &&
                !string.Equals(result.SuggestedNextStep, "No action needed.", StringComparison.Ordinal))
            {
                builder.Append("Next: ").AppendLine(result.SuggestedNextStep);
            }
        }

        if (report.MissingToolNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Missing tools: ").AppendLine(string.Join(", ", report.MissingToolNames));
        }

        if (!string.IsNullOrWhiteSpace(report.StderrTail))
        {
            builder.AppendLine();
            builder.AppendLine("Stderr tail:");
            builder.AppendLine(report.StderrTail);
        }

        return builder.ToString().TrimEnd();
    }
}
