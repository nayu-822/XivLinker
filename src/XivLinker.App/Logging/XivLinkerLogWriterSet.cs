namespace XivLinker.App.Logging;

public sealed class XivLinkerLogWriterSet : IDisposable
{
    public XivLinkerLogWriterSet(
        XivLinkerFileLogWriter appWriter,
        XivLinkerFileLogWriter webSocketWriter)
    {
        AppWriter = appWriter;
        WebSocketWriter = webSocketWriter;
    }

    public XivLinkerFileLogWriter AppWriter { get; }

    public XivLinkerFileLogWriter WebSocketWriter { get; }

    public void Dispose()
    {
        AppWriter.Dispose();
        WebSocketWriter.Dispose();
    }
}
