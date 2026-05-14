using System.Text;
using System.Text.Json;

namespace ExcelMcp.Core.Logging;

public sealed class GridPilotMutableLogger : IGridPilotLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly object _gate = new();
    private StreamWriter? _writer;
    private bool _disposed;

    public GridPilotMutableLogger(GridPilotLogLevel level, string? logPath)
    {
        if (level != GridPilotLogLevel.Off && string.IsNullOrWhiteSpace(logPath))
        {
            throw new InvalidOperationException("A log path is required when runtime logging is enabled.");
        }

        Level = level;
        LogPath = logPath;
        if (level != GridPilotLogLevel.Off)
        {
            EnsureWriter();
        }
    }

    public string? LogPath { get; private set; }

    public GridPilotLogLevel Level { get; private set; }

    public bool IsEnabled(GridPilotLogLevel level) =>
        !_disposed &&
        level != GridPilotLogLevel.Off &&
        Level != GridPilotLogLevel.Off &&
        level <= Level;

    public void UpdateLevel(GridPilotLogLevel level, string? logPath = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!string.IsNullOrWhiteSpace(logPath))
            {
                LogPath = logPath;
            }

            if (level != GridPilotLogLevel.Off && string.IsNullOrWhiteSpace(LogPath))
            {
                throw new InvalidOperationException("A log path is required when runtime logging is enabled.");
            }

            Level = level;

            if (level == GridPilotLogLevel.Off)
            {
                _writer?.Dispose();
                _writer = null;
                return;
            }

            EnsureWriter();
        }
    }

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
            if (!IsEnabled(level))
            {
                return;
            }

            EnsureWriter();
            _writer!.WriteLine(line);
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
            _writer?.Dispose();
            _writer = null;
            return ValueTask.CompletedTask;
        }
    }

    private void EnsureWriter()
    {
        if (_writer is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(LogPath))
        {
            throw new InvalidOperationException("A log path is required when runtime logging is enabled.");
        }

        var directory = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }
}
