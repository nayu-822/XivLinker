using Microsoft.Extensions.Logging;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Models;

namespace XivLinker.Infrastructure.Overlay.Services;

public sealed class OverlayPluginCurrentPlayerStateService : IOverlayPluginCurrentPlayerStateService, IDisposable
{
    private readonly IOverlayPluginWebSocketSessionService sessionService;
    private readonly ILuminaGameDataProvider luminaGameDataProvider;
    private readonly ILogger<OverlayPluginCurrentPlayerStateService> logger;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private CancellationTokenSource? pollingCancellationTokenSource;
    private Task? pollingTask;
    private bool hasLoggedZoneEventPayload;
    private string? primaryPlayerName;
    private uint? currentTerritoryTypeId;
    private uint? currentMapId;
    private string? currentZoneName;
    private CurrentPlayerState currentState = CreateUnavailableState("接続待ち");

    public OverlayPluginCurrentPlayerStateService(
        IOverlayPluginWebSocketSessionService sessionService,
        ILuminaGameDataProvider luminaGameDataProvider,
        ILogger<OverlayPluginCurrentPlayerStateService> logger)
    {
        this.sessionService = sessionService;
        this.luminaGameDataProvider = luminaGameDataProvider;
        this.logger = logger;
        this.sessionService.ConnectionStateChanged += OnSessionConnectionStateChanged;
        this.sessionService.EventReceived += OnSessionEventReceived;
    }

    public event EventHandler? StateChanged;

    public CurrentPlayerState CurrentState => currentState;

    public void Dispose()
    {
        sessionService.ConnectionStateChanged -= OnSessionConnectionStateChanged;
        sessionService.EventReceived -= OnSessionEventReceived;
        refreshGate.Dispose();
        pollingCancellationTokenSource?.Cancel();
        pollingCancellationTokenSource?.Dispose();
    }

    private void OnSessionConnectionStateChanged(object? sender, EventArgs e)
    {
        if (sessionService.IsStarted)
        {
            logger.LogInformation("OverlayPlugin current player state polling started.");
            _ = InitializeSubscriptionsAsync();
            return;
        }

        StopPolling();
        primaryPlayerName = null;
        currentTerritoryTypeId = null;
        currentMapId = null;
        currentZoneName = null;
        UpdateState(CreateUnavailableState("切断中"));
    }

    private void OnSessionEventReceived(object? sender, string rawJson)
    {
        if (!OverlayPluginMessageParser.TryParseEventMessage(rawJson, out OverlayPluginEventMessage? message) || message is null)
        {
            return;
        }

        logger.LogDebug("OverlayPlugin event parsed: {MessageType}", message.MessageType);

        if (OverlayPluginMessageParser.TryParsePrimaryPlayer(message, out string playerName))
        {
            primaryPlayerName = playerName;
            _ = RefreshCurrentPlayerStateAsync();
            return;
        }

        if (OverlayPluginMessageParser.TryParseChangeZone(message, out uint territoryTypeId, out string zoneName))
        {
            logger.LogDebug(
                "ChangeZone parsed. TerritoryTypeId: {TerritoryTypeId}, ZoneName: {ZoneName}",
                territoryTypeId,
                zoneName);

            if (!hasLoggedZoneEventPayload)
            {
                hasLoggedZoneEventPayload = true;
                logger.LogInformation("Received ChangeZone payload: {Payload}", message.RawJson);
            }

            currentTerritoryTypeId = territoryTypeId > 0 ? territoryTypeId : currentTerritoryTypeId;
            currentZoneName = string.IsNullOrWhiteSpace(zoneName) ? currentZoneName : zoneName;
            _ = RefreshCurrentPlayerStateAsync();
        }
    }

    private async Task InitializeSubscriptionsAsync()
    {
        try
        {
            await sessionService.SubscribeAsync(["ChangeZone", "ChangePrimaryPlayer"]);
            logger.LogInformation("OverlayPlugin events subscribed: ChangeZone, ChangePrimaryPlayer");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OverlayPlugin event subscription failed.");
        }

        try
        {
            await sessionService.SendCommandAsync("startOverlayEvents");
            logger.LogInformation("OverlayPlugin startOverlayEvents requested.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OverlayPlugin startOverlayEvents request failed.");
        }

        StartPolling();
    }

    private void StartPolling()
    {
        StopPolling();
        pollingCancellationTokenSource = new CancellationTokenSource();
        pollingTask = Task.Run(() => PollCurrentPlayerStateAsync(pollingCancellationTokenSource.Token));
    }

    private void StopPolling()
    {
        pollingCancellationTokenSource?.Cancel();
        pollingCancellationTokenSource?.Dispose();
        pollingCancellationTokenSource = null;
        pollingTask = null;
    }

    private async Task PollCurrentPlayerStateAsync(CancellationToken cancellationToken)
    {
        UpdateState(CreateUnavailableState("現在状態を取得中"));

        while (!cancellationToken.IsCancellationRequested && sessionService.IsStarted)
        {
            try
            {
                await RefreshCurrentPlayerStateAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to refresh current player state.");
                UpdateState(new CurrentPlayerState
                {
                    IsConnected = currentState.IsConnected,
                    PlayerName = currentState.PlayerName,
                    TerritoryTypeId = currentState.TerritoryTypeId,
                    MapId = currentState.MapId,
                    RawX = currentState.RawX,
                    RawY = currentState.RawY,
                    RawZ = currentState.RawZ,
                    MapX = currentState.MapX,
                    MapY = currentState.MapY,
                    ClassJobId = currentState.ClassJobId,
                    ClassJobName = currentState.ClassJobName,
                    Level = currentState.Level,
                    MapName = currentState.MapName,
                    CoordinatesText = currentState.CoordinatesText,
                    UpdatedAt = currentState.UpdatedAt,
                    IssueMessage = "現在状態の取得に失敗しました。",
                });
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    private async Task RefreshCurrentPlayerStateAsync(CancellationToken cancellationToken = default)
    {
        if (!sessionService.IsStarted)
        {
            UpdateState(CreateUnavailableState("接続待ち"));
            return;
        }

        await refreshGate.WaitAsync(cancellationToken);

        try
        {
            string response = await sessionService.SendRequestAsync("getCombatants", cancellationToken: cancellationToken);
            OverlayCurrentPlayerSnapshot? snapshot = OverlayPluginMessageParser.TryParseCurrentPlayerSnapshot(response, primaryPlayerName);
            if (snapshot is null)
            {
                UpdateState(CreateUnavailableState("現在プレイヤーを未取得"));
                return;
            }

            primaryPlayerName = snapshot.PlayerName;
            currentTerritoryTypeId = snapshot.TerritoryTypeId > 0 ? snapshot.TerritoryTypeId : currentTerritoryTypeId;
            currentMapId = snapshot.MapId > 0 ? snapshot.MapId : currentMapId;
            uint? territoryTypeId = currentTerritoryTypeId;
            uint? mapId = currentMapId;

            string mapName = !string.IsNullOrWhiteSpace(currentZoneName) ? currentZoneName! : "未取得";
            double? mapX = null;
            double? mapY = null;
            string coordinatesText = "未取得";
            string classJobName = "未取得";
            string? issueMessage = null;

            if (territoryTypeId is null or 0)
            {
                issueMessage = "ChangeZone を受信していないため TerritoryTypeId が未取得です。";
                logger.LogDebug("TerritoryTypeId is not available yet.");
            }

            if (snapshot.RawX == 0 && snapshot.RawY == 0 && snapshot.RawZ == 0)
            {
                issueMessage ??= "ワールド座標が未取得です。";
                logger.LogDebug("World coordinates are not available from getCombatants.");
            }

            if ((territoryTypeId is not null and > 0) || (mapId is not null and > 0))
            {
                ResolvedMapLocation? location = await luminaGameDataProvider.ResolveMapLocationAsync(
                    territoryTypeId,
                    mapId,
                    snapshot.RawX,
                    snapshot.RawY,
                    snapshot.RawZ,
                    cancellationToken);

                if (location is not null)
                {
                    mapName = string.IsNullOrWhiteSpace(location.MapName) ? mapName : location.MapName;
                    mapId = location.MapId;
                    mapX = location.MapX;
                    mapY = location.MapY;
                    coordinatesText = location.CoordinatesText;
                    issueMessage = location.IssueMessage;
                }
                else
                {
                    coordinatesText = "マップ情報を取得できません";
                    issueMessage = territoryTypeId is null or 0
                        ? "Lumina 解決に必要な TerritoryTypeId が未取得です。"
                        : "Lumina から TerritoryType / Map を解決できませんでした。";
                }
            }

            if (snapshot.ClassJobId is not null and > 0)
            {
                ResolvedClassJobInfo? jobInfo = await luminaGameDataProvider.ResolveClassJobAsync(
                    snapshot.ClassJobId.Value,
                    snapshot.Level,
                    cancellationToken);

                if (jobInfo is not null)
                {
                    classJobName = jobInfo.DisplayText;
                    issueMessage ??= jobInfo.IssueMessage;
                }
                else
                {
                    classJobName = $"Job ID: {snapshot.ClassJobId.Value}";
                }
            }

            UpdateState(new CurrentPlayerState
            {
                IsConnected = true,
                PlayerName = snapshot.PlayerName,
                TerritoryTypeId = territoryTypeId,
                MapId = mapId,
                RawX = snapshot.RawX,
                RawY = snapshot.RawY,
                RawZ = snapshot.RawZ,
                MapX = mapX,
                MapY = mapY,
                ClassJobId = snapshot.ClassJobId,
                ClassJobName = classJobName,
                Level = snapshot.Level,
                MapName = string.IsNullOrWhiteSpace(mapName) ? "未取得" : mapName,
                CoordinatesText = coordinatesText,
                UpdatedAt = DateTimeOffset.Now,
                IssueMessage = issueMessage,
            });
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private void UpdateState(CurrentPlayerState nextState)
    {
        currentState = nextState;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CurrentPlayerState CreateUnavailableState(string reason)
    {
        return new CurrentPlayerState
        {
            IsConnected = false,
            MapName = reason,
            CoordinatesText = "未取得",
            ClassJobName = "未取得",
            IssueMessage = null,
        };
    }
}
