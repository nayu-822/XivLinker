using Microsoft.Extensions.Logging;

namespace XivLinker.App.Logging;

public sealed class XivLinkerFileLogger : ILogger
{
    private readonly string categoryName;
    private readonly XivLinkerFileLoggerProvider provider;
    private readonly XivLinkerFileLogWriter writer;

    public XivLinkerFileLogger(
        string categoryName,
        XivLinkerFileLoggerProvider provider,
        XivLinkerFileLogWriter writer)
    {
        this.categoryName = categoryName;
        this.provider = provider;
        this.writer = writer;
    }

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return provider.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message))
        {
            message = exception?.Message ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        DateTime timestamp = DateTime.Now;
        string entry = $"{timestamp:yyyy-MM-dd HH:mm:ss.fff} [{XivLinkerFileLoggerProvider.GetDisplayLevel(logLevel)}] {categoryName} - {message}";
        if (exception is not null)
        {
            entry = $"{entry}{Environment.NewLine}{exception}";
        }

        writer.Enqueue(timestamp, entry);
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
