using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Infrastructure.Logging;

/// <summary>
/// Extension methods for custom logging in ActivityPub
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds custom logging services with enhanced capabilities
    /// </summary>
    public static ILoggingBuilder AddCustomLogging(this ILoggingBuilder builder, LogLevel minLogLevel = LogLevel.Information)
    {
        builder.ClearProviders();
        builder.AddProvider(new CustomLoggerProvider(minLogLevel));
        return builder;
    }
}