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
        hasLoggedZoneEventPayload = false;
        UpdateState(CreateUnavailableState("切断中"));
    }

    private void OnSessionEventReceived(object? sender, string rawJson)
    {
        logger.LogDebug(
            "CurrentPlayerStateService received OverlayPlugin event raw JSON: {RawJson}",
            rawJson);

        if (!OverlayPluginMessageParser.TryParseEventMessage(rawJson, out OverlayPluginEventMessage? message) || message is null)
        {
            logger.LogWarning(
                "CurrentPlayerStateService could not parse OverlayPlugin event.");
            logger.LogDebug("Unparseable OverlayPlugin event raw JSON: {RawJson}", rawJson);
            return;
        }

        logger.LogInformation(
            "CurrentPlayerStateService parsed OverlayPlugin event. MessageType: {MessageType}",
            message.MessageType);
        logger.LogDebug(
            "CurrentPlayerStateService parsed OverlayPlugin event payload. MessageType: {MessageType}, Payload: {Payload}, RawJson: {RawJson}",
            message.MessageType,
            message.Payload.GetRawText(),
            message.RawJson);

        if (OverlayPluginMessageParser.TryParsePrimaryPlayer(message, out string playerName))
        {
            primaryPlayerName = playerName;
            _ = RefreshCurrentPlayerStateAsync();
            return;
        }

        if (!OverlayPluginMessageParser.TryParseChangeZone(message, out uint territoryTypeId, out string zoneName))
        {
            logger.LogWarning(
                "ChangeZone event received but payload could not be parsed. MessageType: {MessageType}",
                message.MessageType);
            logger.LogDebug(
                "Unparseable ChangeZone payload. MessageType: {MessageType}, Payload: {Payload}, RawJson: {RawJson}",
                message.MessageType,
                message.Payload.GetRawText(),
                message.RawJson);
            return;
        }

        logger.LogInformation(
            "ChangeZone parsed successfully. TerritoryTypeId: {TerritoryTypeId}, ZoneName: {ZoneName}",
            territoryTypeId,
            zoneName);
        logger.LogDebug("ChangeZone raw event JSON: {RawJson}", message.RawJson);

        currentTerritoryTypeId = territoryTypeId > 0 ? territoryTypeId : currentTerritoryTypeId;
        currentZoneName = string.IsNullOrWhiteSpace(zoneName) ? currentZoneName : zoneName;

        if (!hasLoggedZoneEventPayload)
        {
            hasLoggedZoneEventPayload = true;
        }

        logger.LogInformation(
            "Current zone state updated. CurrentTerritoryTypeId: {CurrentTerritoryTypeId}, CurrentZoneName: {CurrentZoneName}",
            currentTerritoryTypeId,
            currentZoneName);

        _ = RefreshCurrentPlayerStateAsync();
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
        UpdateState(CreateUnavailableState("現在地を取得中"));

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
                    IssueMessage = "現在地更新に失敗しました。",
                });
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    private async Task RefreshCurrentPlayerStateAsync(CancellationToken cancellationToken = default)
    {
        if (!sessionService.IsStarted)
        {
            UpdateState(CreateUnavailableState("未接続"));
            return;
        }

        await refreshGate.WaitAsync(cancellationToken);

        try
        {
            string response = await sessionService.SendRequestAsync(
                "getCombatants",
                cancellationToken: cancellationToken);
            OverlayCurrentPlayerSnapshot? snapshot = OverlayPluginMessageParser.TryParseCurrentPlayerSnapshot(response, primaryPlayerName);
            if (snapshot is null)
            {
                UpdateState(CreateUnavailableState("現在プレイヤーを取得できません"));
                return;
            }

            logger.LogDebug(
                "Current combatant snapshot. Name: {Name}, CombatantTerritoryTypeId: {CombatantTerritoryTypeId}, CombatantMapId: {CombatantMapId}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}, Job: {Job}, Level: {Level}",
                snapshot.PlayerName,
                snapshot.CombatantTerritoryTypeId,
                snapshot.CombatantMapId,
                snapshot.RawX,
                snapshot.RawY,
                snapshot.RawZ,
                snapshot.ClassJobId,
                snapshot.Level);
            logger.LogDebug("Selected current player combatant raw JSON: {CombatantJson}", snapshot.RawCombatantJson);

            primaryPlayerName = snapshot.PlayerName;

            uint? territoryTypeId = currentTerritoryTypeId;
            uint? mapId = currentMapId;
            string? zoneName = currentZoneName;

            string mapName = !string.IsNullOrWhiteSpace(zoneName) ? zoneName! : "未取得";
            double? mapX = null;
            double? mapY = null;
            string coordinatesText = "未取得";
            string classJobName = "未取得";
            string? issueMessage = null;
            bool hasMapResolutionKey =
                territoryTypeId is > 0
                || mapId is > 0
                || !string.IsNullOrWhiteSpace(zoneName);

            if (snapshot.RawX == 0 && snapshot.RawY == 0 && snapshot.RawZ == 0)
            {
                issueMessage ??= "ワールド座標が未取得です。";
                logger.LogWarning(
                    "Current player coordinates are zero. getCombatants response sample: {ResponseSample}",
                    response.Length > 4000 ? response[..4000] : response);
            }

            if (!hasMapResolutionKey)
            {
                coordinatesText = "ChangeZone 待ち";
                issueMessage = "ChangeZone をまだ受信していないため、マップ座標を解決できません。";
                logger.LogInformation(
                    "Skipping map coordinate conversion because zone context is not available. Player: {PlayerName}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}",
                    snapshot.PlayerName,
                    snapshot.RawX,
                    snapshot.RawY,
                    snapshot.RawZ);
            }
            else
            {
                logger.LogInformation(
                    "Map conversion input. TerritoryTypeId: {TerritoryTypeId}, MapId: {MapId}, ZoneName: {ZoneName}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}",
                    territoryTypeId,
                    mapId,
                    zoneName,
                    snapshot.RawX,
                    snapshot.RawY,
                    snapshot.RawZ);

                logger.LogDebug(
                    "Before map coordinate conversion. TerritoryTypeId: {TerritoryTypeId}, MapId: {MapId}, MapName: {MapName}, MapSourceX: {RawX}, MapSourceY: {RawY}, RawZ: {RawZ}",
                    territoryTypeId,
                    mapId,
                    mapName,
                    snapshot.RawX,
                    snapshot.RawY,
                    snapshot.RawZ);

                ResolvedMapLocation? location = await luminaGameDataProvider.ResolveMapLocationAsync(
                    territoryTypeId,
                    mapId,
                    mapName,
                    snapshot.RawX,
                    snapshot.RawY,
                    snapshot.RawZ,
                    cancellationToken);

                if (location is not null)
                {
                    logger.LogDebug(
                        "After map coordinate conversion. MapName: {MapName}, MapId: {MapId}, MapX: {MapX}, MapY: {MapY}, CoordinatesText: {CoordinatesText}, Issue: {IssueMessage}",
                        location.MapName,
                        location.MapId,
                        location.MapX,
                        location.MapY,
                        location.CoordinatesText,
                        location.IssueMessage);

                    if (!string.IsNullOrWhiteSpace(location.MapName)
                        && !location.MapName.StartsWith("Territory ID:", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(location.MapName, "未取得", StringComparison.Ordinal))
                    {
                        mapName = location.MapName;
                    }

                    mapId = location.MapId > 0 ? location.MapId : mapId;
                    mapX = location.MapX;
                    mapY = location.MapY;
                    coordinatesText = location.CoordinatesText;
                    issueMessage = location.IssueMessage;
                }
                else
                {
                    coordinatesText = "マップ情報を取得できません";
                    issueMessage = "Lumina から TerritoryType / Map を解決できませんでした。";
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
