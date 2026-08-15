using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ActivityPub.Core.Infrastructure.Logging;

/// <summary>
/// Custom logger provider for ActivityPub with enhanced capabilities
/// </summary>
public class CustomLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, CustomLogger> _loggers = new();
    private readonly LogLevel _minLogLevel;

    public CustomLoggerProvider(LogLevel minLogLevel = LogLevel.Information)
    {
        _minLogLevel = minLogLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new CustomLogger(name, _minLogLevel));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}

/// <summary>
/// Custom logger implementation with enhanced features
/// </summary>
public class CustomLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LogLevel _minLogLevel;

    public CustomLogger(string categoryName, LogLevel minLogLevel)
    {
        _categoryName = categoryName;
        _minLogLevel = minLogLevel;
    }

    IDisposable ILogger.BeginScope<TState>(TState state) => null!;

    public bool IsEnabled(LogLevel level) => level >= _minLogLevel;

    public void Log<TState>(
        LogLevel level,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level))
            return;

        var message = formatter(state, exception);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        // Enhanced structured logging format
        var logEntry = $"[{timestamp}] [{level}] [{_categoryName}] {message}";
        
        if (exception != null)
        {
            logEntry += $"\nException: {exception}";
        }
        
        // Write to console (in production, this could be sent to a logging service)
        Console.WriteLine(logEntry);
    }
}