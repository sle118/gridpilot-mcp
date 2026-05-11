using ExcelMcp.Deployment.Logs;
using Xunit;

namespace GridPilot.Tray.Tests;

public sealed class LogPresenterTests
{
    [Fact]
    public void FormatLogMetadata_IncludesExistingLogMetadata()
    {
        var timestamp = new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
        var log = new DeploymentLogEntry(
            DeploymentLogKind.HostConventional,
            @"C:\logs\gridpilot-runtime.log",
            Exists: true,
            SizeBytes: 42,
            LastWriteTimeUtc: timestamp,
            DeploymentLogAccessStatus.Accessible);

        var text = LogPresenter.FormatLogMetadata(log);

        Assert.Contains("Kind: HostConventional", text, StringComparison.Ordinal);
        Assert.Contains("Exists: True", text, StringComparison.Ordinal);
        Assert.Contains("Size: 42 bytes", text, StringComparison.Ordinal);
        Assert.Contains("Modified UTC: 2026-05-09 12:00:00Z", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLogMetadata_IncludesMissingOrUnreadableMessage()
    {
        var log = new DeploymentLogEntry(
            DeploymentLogKind.ProfileConfigured,
            @"C:\logs\missing.log",
            Exists: false,
            SizeBytes: null,
            LastWriteTimeUtc: null,
            DeploymentLogAccessStatus.Unreadable,
            "Access denied.");

        var text = LogPresenter.FormatLogMetadata(log);

        Assert.Contains("Access: Unreadable", text, StringComparison.Ordinal);
        Assert.Contains("Size: (unknown)", text, StringComparison.Ordinal);
        Assert.Contains("Message: Access denied.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTail_IncludesTruncationAndLines()
    {
        var tail = new RecentLogReadResult(
            @"C:\logs\gridpilot-runtime.log",
            Exists: true,
            DeploymentLogAccessStatus.Accessible,
            ["line 1", "line 2"],
            WasTruncated: true);

        var text = LogPresenter.FormatTail(tail);

        Assert.Contains("Tail was truncated", text, StringComparison.Ordinal);
        Assert.Contains("line 1", text, StringComparison.Ordinal);
        Assert.Contains("line 2", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTail_EmptyTailUsesReadablePlaceholder()
    {
        var tail = new RecentLogReadResult(
            @"C:\logs\empty.log",
            Exists: true,
            DeploymentLogAccessStatus.Accessible,
            [],
            WasTruncated: false);

        var text = LogPresenter.FormatTail(tail);

        Assert.Contains("(empty)", text, StringComparison.Ordinal);
    }
}
