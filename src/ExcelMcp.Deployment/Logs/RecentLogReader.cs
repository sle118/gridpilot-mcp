using System.Text;

namespace ExcelMcp.Deployment.Logs;

public static class RecentLogReader
{
    public static async Task<RecentLogReadResult> ReadTailAsync(
        string path,
        RecentLogReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        options ??= RecentLogReadOptions.Default;
        if (options.MaxLines <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxLines must be greater than zero.");
        }

        if (options.MaxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxBytes must be greater than zero.");
        }

        if (!File.Exists(path))
        {
            return new RecentLogReadResult(
                path,
                Exists: false,
                DeploymentLogAccessStatus.Missing,
                Array.Empty<string>(),
                WasTruncated: false,
                "Log file does not exist.");
        }

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            if (stream.Length == 0)
            {
                return new RecentLogReadResult(
                    path,
                    Exists: true,
                    DeploymentLogAccessStatus.Accessible,
                    Array.Empty<string>(),
                    WasTruncated: false);
            }

            var readLength = (int)Math.Min(stream.Length, options.MaxBytes);
            var start = stream.Length - readLength;
            var buffer = new byte[readLength];
            stream.Seek(start, SeekOrigin.Begin);

            var totalRead = 0;
            while (totalRead < readLength)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, readLength - totalRead), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, totalRead)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            var lines = text.Split('\n').ToList();
            if (lines.Count > 0 && lines[^1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            var truncatedByBytes = start > 0;
            if (truncatedByBytes && lines.Count > 0)
            {
                lines.RemoveAt(0);
            }

            var truncatedByLines = lines.Count > options.MaxLines;
            if (truncatedByLines)
            {
                lines = lines.Skip(lines.Count - options.MaxLines).ToList();
            }

            return new RecentLogReadResult(
                path,
                Exists: true,
                DeploymentLogAccessStatus.Accessible,
                lines,
                truncatedByBytes || truncatedByLines);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new RecentLogReadResult(
                path,
                Exists: true,
                DeploymentLogAccessStatus.Unreadable,
                Array.Empty<string>(),
                WasTruncated: false,
                exception.Message);
        }
    }
}
