using System.Text;
using System.Text.Json;

namespace ExcelMcp.Core.Logging;

public sealed class GridPilotFileLogger : IGridPilotLogger, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly StreamWriter _writer;
    private readonly object _gate = new();
    private bool _disposed;

    public GridPilotFileLogger(string logPath, GridPilotLogLevel level)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            throw new InvalidOperationException("A log path is required when runtime logging is enabled.");
        }

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
        {
            AutoFlush = true
        };

        LogPath = logPath;
        Level = level;
    }

    public string LogPath { get; }

    public GridPilotLogLevel Level { get; }

    public bool IsEnabled(GridPilotLogLevel level) =>
        !_disposed &&
        level != GridPilotLogLevel.Off &&
        Level != GridPilotLogLevel.Off &&
        level <= Level;

    public void Log(
        GridPilotLogLevel level,
        string category,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null,
        Exception? exception = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["timestampUtc"] = DateTimeOffset.UtcNow,
            ["level"] = level.ToString().ToLowerInvariant(),
            ["category"] = category,
            ["event"] = eventName
        };

        if (fields is not null)
        {
            foreach (var entry in fields)
            {
                payload[entry.Key] = entry.Value;
            }
        }

        if (exception is not null)
        {
            payload["exceptionType"] = exception.GetType().FullName;
            payload["exceptionMessage"] = exception.Message;
        }

        var line = JsonSerializer.Serialize(payload, JsonOptions);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.WriteLine(line);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            _writer.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
