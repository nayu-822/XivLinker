using Microsoft.Extensions.Logging;

namespace XivLinker.Application.Logging;

public enum XivLinkerLogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}

public static class XivLinkerLogLevelExtensions
{
    public static string ToDisplayName(this XivLinkerLogLevel level)
    {
        return level switch
        {
            XivLinkerLogLevel.Debug => "DEBUG",
            XivLinkerLogLevel.Info => "INFO",
            XivLinkerLogLevel.Warn => "WARN",
            XivLinkerLogLevel.Error => "ERROR",
            _ => "INFO",
        };
    }

    public static LogLevel ToMicrosoftLogLevel(this XivLinkerLogLevel level)
    {
        return level switch
        {
            XivLinkerLogLevel.Debug => LogLevel.Debug,
            XivLinkerLogLevel.Info => LogLevel.Information,
            XivLinkerLogLevel.Warn => LogLevel.Warning,
            XivLinkerLogLevel.Error => LogLevel.Error,
            _ => LogLevel.Information,
        };
    }
}
