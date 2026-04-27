using System.Collections.Concurrent;

namespace Dogebot.Server.Services;

/// <summary>
/// Thread-safe in-memory ring buffer for debug log entries.
/// </summary>
public class DebugLogService
{
    private readonly ConcurrentQueue<DebugLogEntry> _entries = new();
    private const int MaxEntries = 200;

    public void Log(string category, string message)
    {
        var entry = new DebugLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)),
            Category = category,
            Message = message
        };

        _entries.Enqueue(entry);

        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);
    }

    public List<DebugLogEntry> GetRecent(int count)
    {
        return _entries.Reverse().Take(count).Reverse().ToList();
    }

    public int Count => _entries.Count;
}

public class DebugLogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }

    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] [{Category}] {Message}";
    }
}

