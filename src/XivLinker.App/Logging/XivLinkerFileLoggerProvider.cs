using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Logging;

namespace XivLinker.App.Logging;

public sealed class XivLinkerFileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, XivLinkerFileLogger> loggers = new(StringComparer.Ordinal);
    private readonly IAppSettingsStore appSettingsStore;
    private readonly XivLinkerFileLogWriter writer;
    private volatile LogLevel minimumLevel;

    public XivLinkerFileLoggerProvider(
        IAppSettingsStore appSettingsStore,
        XivLinkerFileLogWriter writer)
    {
        this.appSettingsStore = appSettingsStore;
        this.writer = writer;
        minimumLevel = appSettingsStore.Current.FileLogLevel.ToMicrosoftLogLevel();
        appSettingsStore.SettingsChanged += OnSettingsChanged;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return loggers.GetOrAdd(categoryName, name => new XivLinkerFileLogger(name, this, writer));
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
        {
            return false;
        }

        return Normalize(logLevel) >= minimumLevel;
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

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        minimumLevel = appSettingsStore.Current.FileLogLevel.ToMicrosoftLogLevel();
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
