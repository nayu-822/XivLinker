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
    private string? primaryPlayerName;
    private uint? currentTerritoryTypeId;
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
            StartPolling();
            return;
        }

        StopPolling();
        primaryPlayerName = null;
        currentTerritoryTypeId = null;
        currentZoneName = null;
        UpdateState(CreateUnavailableState("切断中"));
    }

    private void OnSessionEventReceived(object? sender, string rawJson)
    {
        if (!OverlayPluginMessageParser.TryParseEventMessage(rawJson, out OverlayPluginEventMessage? message) || message is null)
        {
            return;
        }

        if (OverlayPluginMessageParser.TryParsePrimaryPlayer(message, out string playerName))
        {
            primaryPlayerName = playerName;
            _ = RefreshCurrentPlayerStateAsync();
            return;
        }

        if (OverlayPluginMessageParser.TryParseChangeZone(message, out uint territoryTypeId, out string zoneName))
        {
            currentTerritoryTypeId = territoryTypeId > 0 ? territoryTypeId : currentTerritoryTypeId;
            currentZoneName = string.IsNullOrWhiteSpace(zoneName) ? currentZoneName : zoneName;
            _ = RefreshCurrentPlayerStateAsync();
        }
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
            uint? territoryTypeId = currentTerritoryTypeId;

            string mapName = !string.IsNullOrWhiteSpace(currentZoneName) ? currentZoneName! : "未取得";
            uint? mapId = null;
            double? mapX = null;
            double? mapY = null;
            string coordinatesText = "未取得";
            string classJobName = "未取得";
            string? issueMessage = null;

            if (territoryTypeId is not null and > 0)
            {
                ResolvedMapLocation? location = await luminaGameDataProvider.ResolveMapLocationAsync(
                    territoryTypeId.Value,
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
                    issueMessage = "Lumina からマップ情報を解決できませんでした。";
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
