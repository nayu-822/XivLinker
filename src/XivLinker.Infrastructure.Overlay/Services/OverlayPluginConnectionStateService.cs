using System.Text.Json;
using XivLinker.Infrastructure.Lumina.Services;

namespace XivLinker.Infrastructure.Overlay.Services;

public sealed class OverlayPluginConnectionStateService : IDisposable
{
    private readonly SemaphoreSlim connectGate = new(1, 1);
    private readonly IOverlayPluginWebSocketService webSocketService;
    private readonly IOverlayPluginWebSocketSessionService sessionService;
    private readonly IGameDataService gameDataService;

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
        Message = "ACT または OverlayPlugin の WebSocket サーバーに接続していません。";
    }

    public event EventHandler? StateChanged;

    public OverlayPluginConnectionState State
    {
        get; private set;
    }

    public string Message
    {
        get; private set;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await connectGate.WaitAsync(cancellationToken);

        try
        {
            SetState(
                OverlayPluginConnectionState.Connecting,
                "OverlayPlugin WebSocket に接続しています。");

            bool canConnect = await webSocketService.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                SetState(
                    OverlayPluginConnectionState.Unavailable,
                    "OverlayPlugin WebSocket サーバーが利用できません。");
                return;
            }

            GameDataStatus gameDataStatus = await gameDataService.CheckAvailabilityAsync(cancellationToken);
            await sessionService.StartAsync(cancellationToken);

            string? versionText = await GetVersionTextAsync(cancellationToken);
            string luminaText = gameDataStatus.IsAvailable
                ? "Lumina 利用可能"
                : "Lumina 未初期化";

            SetState(
                OverlayPluginConnectionState.Connected,
                versionText is null
                    ? $"OverlayPlugin WebSocket に接続しました。{luminaText}。"
                    : $"OverlayPlugin WebSocket に接続しました。Version: {versionText} / {luminaText}。");
        }
        catch (TimeoutException)
        {
            SetState(
                OverlayPluginConnectionState.Unavailable,
                "OverlayPlugin WebSocket から応答がありませんでした。");
            throw;
        }
        catch (OperationCanceledException)
        {
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

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await connectGate.WaitAsync(cancellationToken);

        try
        {
            await sessionService.StopAsync(cancellationToken);
            SetState(
                OverlayPluginConnectionState.Disconnected,
                "OverlayPlugin WebSocket への接続を停止しました。");
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
            "OverlayPlugin WebSocket から切断されました。");
    }

    private void SetState(OverlayPluginConnectionState state, string message)
    {
        State = state;
        Message = message;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
