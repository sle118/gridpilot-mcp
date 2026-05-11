using System.Text;
using ExcelMcp.Deployment.Logs;

namespace GridPilot.Tray;

internal static class LogPresenter
{
    public static string FormatLogMetadata(DeploymentLogEntry log)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Kind: {log.Kind}");
        builder.AppendLine($"Path: {log.Path}");
        builder.AppendLine($"Exists: {log.Exists}");
        builder.AppendLine($"Access: {log.AccessStatus}");
        builder.AppendLine($"Size: {FormatSize(log.SizeBytes)}");
        builder.AppendLine($"Modified UTC: {FormatTime(log.LastWriteTimeUtc)}");
        if (!string.IsNullOrWhiteSpace(log.Message))
        {
            builder.AppendLine($"Message: {log.Message}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatTail(RecentLogReadResult tail)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Path: {tail.Path}");
        builder.AppendLine($"Exists: {tail.Exists}");
        builder.AppendLine($"Access: {tail.AccessStatus}");
        if (!string.IsNullOrWhiteSpace(tail.Message))
        {
            builder.AppendLine($"Message: {tail.Message}");
        }

        if (tail.WasTruncated)
        {
            builder.AppendLine("Tail was truncated by the configured bounds.");
        }

        builder.AppendLine();
        builder.AppendLine(tail.Lines.Count == 0 ? "(empty)" : string.Join(Environment.NewLine, tail.Lines));
        return builder.ToString().TrimEnd();
    }

    public static string? GetExistingParentDirectory(DeploymentLogEntry log)
    {
        var directory = Path.GetDirectoryName(log.Path);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) ? directory : null;
    }

    private static string FormatSize(long? sizeBytes) =>
        sizeBytes is null ? "(unknown)" : $"{sizeBytes.Value} bytes";

    private static string FormatTime(DateTimeOffset? timestamp) =>
        timestamp is null ? "(unknown)" : timestamp.Value.ToString("u");
}
