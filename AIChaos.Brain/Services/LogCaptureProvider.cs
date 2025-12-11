using Microsoft.Extensions.Logging;

namespace AIChaos.Brain.Services;

/// <summary>
/// Logger provider that captures logs to the LogCaptureService.
/// </summary>
public class LogCaptureProvider : ILoggerProvider
{
    private readonly LogCaptureService _logCaptureService;

    public LogCaptureProvider(LogCaptureService logCaptureService)
    {
        _logCaptureService = logCaptureService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new LogCaptureLogger(categoryName, _logCaptureService);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}

/// <summary>
/// Logger implementation that forwards logs to LogCaptureService.
/// </summary>
public class LogCaptureLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogCaptureService _logCaptureService;

    public LogCaptureLogger(string categoryName, LogCaptureService logCaptureService)
    {
        _categoryName = categoryName;
        _logCaptureService = logCaptureService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        _logCaptureService.AddLog(logLevel, _categoryName, message, exception);
    }
}
