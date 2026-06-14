using System.Text.Json;
using XivLinker.Infrastructure.Lumina.Services;

namespace XivLinker.Infrastructure.Overlay.Services;

public sealed class OverlayPluginConnectionStateService : IDisposable
{
    private readonly SemaphoreSlim connectGate = new(1, 1);
    private readonly IOverlayPluginWebSocketService webSocketService;
    private readonly IOverlayPluginWebSocketSessionService sessionService;
    private readonly IGameDataService gameDataService;
    private int autoConnectAttempted;

    public OverlayPluginConnectionStateService(
        IOverlayPluginWebSocketService webSocketService,
        IOverlayPluginWebSocketSessionService sessionService,
        IGameDataService gameDataService)
    {
        this.webSocketService = webSocketService;
        this.sessionService = sessionService;
        this.gameDataService = gameDataService;
        this.sessionService.ConnectionStateChanged += OnSessionConnectionStateChanged;
        State = OverlayPluginConnectionState.Disconnected;
        Message = "ACT \u307E\u305F\u306F OverlayPlugin \u306E WebSocket \u30B5\u30FC\u30D0\u30FC\u306B\u63A5\u7D9A\u3057\u3066\u3044\u307E\u305B\u3093\u3002";
    }

    public event EventHandler? StateChanged;

    public OverlayPluginConnectionState State { get; private set; }

    public string Message { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref autoConnectAttempted, 1) == 1)
        {
            return;
        }

        try
        {
            await ConnectAsync(cancellationToken);
        }
        catch
        {
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await connectGate.WaitAsync(cancellationToken);
        try
        {
            SetState(
                OverlayPluginConnectionState.Connecting,
                "OverlayPlugin WebSocket \u306B\u63A5\u7D9A\u3057\u3066\u3044\u307E\u3059\u3002");

            bool canConnect = await webSocketService.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                SetState(
                    OverlayPluginConnectionState.Unavailable,
                    "OverlayPlugin WebSocket \u30B5\u30FC\u30D0\u30FC\u304C\u5229\u7528\u3067\u304D\u307E\u305B\u3093\u3002");
                return;
            }

            GameDataStatus gameDataStatus = await gameDataService.CheckAvailabilityAsync();
            await sessionService.StartAsync(cancellationToken);

            string? versionText = await GetVersionTextAsync(cancellationToken);
            string luminaText = gameDataStatus.IsAvailable
                ? "Lumina \u5229\u7528\u53EF\u80FD"
                : "Lumina \u672A\u521D\u671F\u5316";

            SetState(
                OverlayPluginConnectionState.Connected,
                versionText is null
                    ? $"OverlayPlugin WebSocket \u306B\u63A5\u7D9A\u3057\u307E\u3057\u305F\u3002{luminaText}\u3002"
                    : $"OverlayPlugin WebSocket \u306B\u63A5\u7D9A\u3057\u307E\u3057\u305F\u3002Version: {versionText} / {luminaText}\u3002");
        }
        catch (TimeoutException)
        {
            SetState(
                OverlayPluginConnectionState.Unavailable,
                "OverlayPlugin WebSocket \u304B\u3089\u5FDC\u7B54\u304C\u3042\u308A\u307E\u305B\u3093\u3067\u3057\u305F\u3002");
            throw;
        }
        catch (Exception exception)
        {
            SetState(OverlayPluginConnectionState.Error, exception.Message);
            throw;
        }
        finally
        {
            connectGate.Release();
        }
    }

    public void Dispose()
    {
        sessionService.ConnectionStateChanged -= OnSessionConnectionStateChanged;
        connectGate.Dispose();
    }

    private async Task<string?> GetVersionTextAsync(CancellationToken cancellationToken)
    {
        string response = await sessionService.SendRequestAsync("getVersion", cancellationToken: cancellationToken);
        using JsonDocument document = JsonDocument.Parse(response);
        return document.RootElement.TryGetProperty("version", out JsonElement versionElement)
            ? versionElement.GetString()
            : null;
    }

    private void OnSessionConnectionStateChanged(object? sender, EventArgs e)
    {
        if (sessionService.IsStarted)
        {
            SetState(OverlayPluginConnectionState.Connected, Message);
            return;
        }

        SetState(
            OverlayPluginConnectionState.Disconnected,
            "OverlayPlugin WebSocket \u304B\u3089\u5207\u65AD\u3055\u308C\u307E\u3057\u305F\u3002");
    }

    private void SetState(OverlayPluginConnectionState state, string message)
    {
        State = state;
        Message = message;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
