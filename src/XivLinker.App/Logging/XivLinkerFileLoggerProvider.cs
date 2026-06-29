using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Logging;

namespace XivLinker.App.Logging;

public sealed class XivLinkerFileLoggerProvider : ILoggerProvider
{
    private const string WebSocketCategoryPrefix = "XivLinker.Infrastructure.Overlay";
    private readonly ConcurrentDictionary<string, XivLinkerFileLogger> loggers = new(StringComparer.Ordinal);
    private readonly IAppSettingsStore appSettingsStore;
    private readonly XivLinkerLogWriterSet writerSet;
    private volatile LogLevel appMinimumLevel;
    private volatile LogLevel webSocketMinimumLevel;

    public XivLinkerFileLoggerProvider(
        IAppSettingsStore appSettingsStore,
        XivLinkerLogWriterSet writerSet)
    {
        this.appSettingsStore = appSettingsStore;
        this.writerSet = writerSet;
        appMinimumLevel = appSettingsStore.Current.FileLogLevel.ToMicrosoftLogLevel();
        webSocketMinimumLevel = appSettingsStore.Current.WebSocketLogLevel.ToMicrosoftLogLevel();
        appSettingsStore.SettingsChanged += OnSettingsChanged;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return loggers.GetOrAdd(categoryName, name => new XivLinkerFileLogger(name, this));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return IsEnabled(string.Empty, logLevel);
    }

    public void Dispose()
    {
        appSettingsStore.SettingsChanged -= OnSettingsChanged;
    }

    internal static string GetDisplayLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "DEBUG",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "ERROR",
            _ => "INFO",
        };
    }

    public bool IsEnabled(string categoryName, LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
        {
            return false;
        }

        return Normalize(logLevel) >= GetMinimumLevel(categoryName);
    }

    internal void Write(string categoryName, LogLevel logLevel, DateTime timestamp, string entry)
    {
        if (!IsEnabled(categoryName, logLevel))
        {
            return;
        }

        GetWriter(categoryName).Enqueue(timestamp, entry);
    }

    internal static bool IsWebSocketCategory(string categoryName)
    {
        return categoryName.StartsWith(WebSocketCategoryPrefix, StringComparison.Ordinal);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        appMinimumLevel = appSettingsStore.Current.FileLogLevel.ToMicrosoftLogLevel();
        webSocketMinimumLevel = appSettingsStore.Current.WebSocketLogLevel.ToMicrosoftLogLevel();
    }

    private LogLevel GetMinimumLevel(string categoryName)
    {
        return IsWebSocketCategory(categoryName)
            ? webSocketMinimumLevel
            : appMinimumLevel;
    }

    private XivLinkerFileLogWriter GetWriter(string categoryName)
    {
        return IsWebSocketCategory(categoryName)
            ? writerSet.WebSocketWriter
            : writerSet.AppWriter;
    }

    private static LogLevel Normalize(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => LogLevel.Debug,
            LogLevel.Critical => LogLevel.Error,
            _ => logLevel,
        };
    }
}
