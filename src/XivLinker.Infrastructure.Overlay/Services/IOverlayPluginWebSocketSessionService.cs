namespace XivLinker.Infrastructure.Overlay.Services;

public interface IOverlayPluginWebSocketSessionService
{
    event EventHandler? ConnectionStateChanged;
    event EventHandler<string>? EventReceived;

    bool IsStarted
    {
        get;
    }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<string> SendRequestAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);
}
