using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AIChaos.Brain.Services;

/// <summary>
/// Service for capturing and storing recent log entries in memory for admin viewing.
/// </summary>
public class LogCaptureService
{
    private readonly ConcurrentQueue<LogEntry> _logEntries = new();
    private const int MAX_LOG_ENTRIES = 1000; // Keep last 1000 logs

    /// <summary>
    /// Adds a log entry to the buffer.
    /// </summary>
    public void AddLog(LogLevel level, string category, string message, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            Exception = exception?.ToString()
        };

        _logEntries.Enqueue(entry);

        // Trim old entries if we exceed max (with buffer to avoid excessive trimming)
        while (_logEntries.Count > MAX_LOG_ENTRIES + 10)
        {
            _logEntries.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Gets all captured log entries in reverse chronological order (newest first).
    /// </summary>
    public IEnumerable<LogEntry> GetLogs()
    {
        return _logEntries.Reverse();
    }

    /// <summary>
    /// Clears all captured log entries.
    /// </summary>
    public void ClearLogs()
    {
        _logEntries.Clear();
    }
}

/// <summary>
/// Represents a captured log entry.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
}
