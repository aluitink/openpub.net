using System.Text;
using ActivityPub.Core.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ActivityPub.Tests.Infrastructure.Logging;

/// <summary>
/// Unit tests for <see cref="CustomLoggerProvider"/> and <see cref="CustomLogger"/>
/// — the custom logging implementation, which previously had no direct unit test.
/// Captures <c>Console.Out</c> to verify the enhanced structured log format and
/// the minimum-log-level gating.
/// </summary>
public class CustomLoggerProviderTests
{
    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _original;
        public readonly StringWriter Output;
        private readonly object _lock = new();

        public ConsoleCapture()
        {
            _original = Console.Out;
            Output = new StringWriter();
            lock (_lock)
            {
                Console.SetOut(Output);
            }
        }

        public string Captured
        {
            get
            {
                lock (_lock)
                {
                    Output.Flush();
                    return Output.ToString();
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                Console.SetOut(_original);
                Output.Dispose();
            }
        }
    }

    [Fact]
    public void CreateLogger_ReturnsSameInstanceForSameCategory()
    {
        using var provider = new CustomLoggerProvider();

        var a = provider.CreateLogger("My.Category");
        var b = provider.CreateLogger("My.Category");
        var c = provider.CreateLogger("Other.Category");

        Assert.Same(a, b); // loggers are cached per category
        Assert.NotSame(a, c); // different categories yield different loggers
    }

    [Fact]
    public void CreateLogger_DefaultMinLevel_Information()
    {
        using var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("test");

        // Default minimum is Information.
        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void IsEnabled_RespectsConfiguredMinLevel()
    {
        using var provider = new CustomLoggerProvider(LogLevel.Warning);
        var logger = provider.CreateLogger("test");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void Log_BelowMinLevel_IsNotWritten()
    {
        using var provider = new CustomLoggerProvider(LogLevel.Warning);
        var logger = provider.CreateLogger("test");
        using var capture = new ConsoleCapture();

        logger.LogInformation("should not appear");

        Assert.DoesNotContain("should not appear", capture.Captured);
    }

    [Fact]
    public void Log_AtMinLevel_WritesEnhancedFormat()
    {
        using var provider = new CustomLoggerProvider(LogLevel.Information);
        var logger = provider.CreateLogger("MyCategory");
        using var capture = new ConsoleCapture();

        logger.LogInformation("hello world");

        var text = capture.Captured;
        // Format: [timestamp] [level] [category] message
        Assert.Contains("[Information]", text);
        Assert.Contains("[MyCategory]", text);
        Assert.Contains("hello world", text);
        Assert.StartsWith("[", text.TrimStart());
    }

    [Fact]
    public void Log_WithException_AppendsExceptionLine()
    {
        using var provider = new CustomLoggerProvider(LogLevel.Error);
        var logger = provider.CreateLogger("MyCategory");
        using var capture = new ConsoleCapture();

        var ex = new InvalidOperationException("boom");
        logger.LogError(ex, "something failed");

        var text = capture.Captured;
        Assert.Contains("[Error]", text);
        Assert.Contains("something failed", text);
        Assert.Contains("Exception:", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void Log_FormatsStateViaFormatter()
    {
        using var provider = new CustomLoggerProvider(LogLevel.Information);
        var logger = provider.CreateLogger("fmt");
        using var capture = new ConsoleCapture();

        logger.LogInformation("value={Value}", 42);

        // The default formatter substitutes the placeholder.
        Assert.Contains("value=42", capture.Captured);
    }

    [Fact]
    public void BeginScope_ReturnsNullAndDoesNotThrow()
    {
        using var provider = new CustomLoggerProvider();
        var logger = provider.CreateLogger("scope");

        var scope = logger.BeginScope("scope-state");

        Assert.Null(scope);
    }

    [Fact]
    public void Dispose_DoesNotThrowAndAllowsSubsequentCreate()
    {
        var provider = new CustomLoggerProvider();
        provider.CreateLogger("a");
        provider.CreateLogger("b");

        // Disposing clears the cache; creating after dispose must not throw.
        provider.Dispose();
        var after = provider.CreateLogger("c");

        Assert.NotNull(after);
    }
}
