using System.Text.Json;

namespace XivLinker.Infrastructure.Overlay.Services;

public interface IOverlayPluginWebSocketService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

    Task<JsonDocument> CallAsync(
        string call,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default);

    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);
}
