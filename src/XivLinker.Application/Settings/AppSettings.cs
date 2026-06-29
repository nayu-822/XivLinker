using XivLinker.Application.Logging;

namespace XivLinker.Application.Settings;

public sealed class AppSettings
{
    public XivLinkerLogLevel FileLogLevel { get; set; } = XivLinkerLogLevel.Info;

    public XivLinkerLogLevel WebSocketLogLevel { get; set; } = XivLinkerLogLevel.Warn;
}
