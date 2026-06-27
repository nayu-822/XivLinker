using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public partial class DataSourceStatusViewModel : ObservableObject
{
    private readonly IGameDataService gameDataService;
    private readonly OverlayPluginConnectionStateService overlayPluginConnectionStateService;
    private readonly AppEventLogViewModel eventLog;
    private readonly ILogger<DataSourceStatusViewModel> logger;
    private readonly DataSourceStatusItemViewModel luminaItem;
    private readonly DataSourceStatusItemViewModel overlayItem;

    public DataSourceStatusViewModel(
        IGameDataService gameDataService,
        OverlayPluginConnectionStateService overlayPluginConnectionStateService,
        AppEventLogViewModel eventLog,
        ILogger<DataSourceStatusViewModel> logger)
    {
        this.gameDataService = gameDataService;
        this.overlayPluginConnectionStateService = overlayPluginConnectionStateService;
        this.eventLog = eventLog;
        this.logger = logger;

        RefreshLuminaCommand = new AsyncRelayCommand(RefreshGameDataStatusAsync, CanRefreshLumina);
        ConnectOverlayPluginCommand = new AsyncRelayCommand(ConnectOverlayPluginAsync, CanConnectOverlayPlugin);

        luminaItem = new DataSourceStatusItemViewModel(
            "Lumina",
            "未設定",
            "FF14 の sqpack フォルダーがまだ設定されていません。",
            "warning",
            "状態を確認",
            RefreshLuminaCommand);
        overlayItem = new DataSourceStatusItemViewModel(
            "OverlayPlugin WebSocket",
            "未接続",
            "ACT または OverlayPlugin の WebSocket サーバーに接続していません。",
            "warning",
            "接続する",
            ConnectOverlayPluginCommand);

        Items = new ObservableCollection<DataSourceStatusItemViewModel>
        {
            overlayItem,
            luminaItem,
            new(
                "キャラクター設定",
                "未設定",
                "キャラクター設定ファイル連携はまだ未実装です。",
                "warning"),
            new(
                "ログ",
                "未設定",
                "FF14 / ACT ログ連携はまだ未実装です。",
                "warning"),
        };

        this.overlayPluginConnectionStateService.StateChanged += OnOverlayPluginStateChanged;
    }

    public ObservableCollection<DataSourceStatusItemViewModel> Items
    {
        get;
    }

    public IAsyncRelayCommand RefreshLuminaCommand
    {
        get;
    }

    public IAsyncRelayCommand ConnectOverlayPluginCommand
    {
        get;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshGameDataStatusAsync(cancellationToken);
        await ConnectOverlayPluginAsync(cancellationToken);
    }

    private async Task RefreshGameDataStatusAsync(CancellationToken cancellationToken = default)
    {
        luminaItem.Status = "確認中...";
        luminaItem.StatusTone = "neutral";
        luminaItem.Detail = "Lumina の初期化状態を確認しています。";

        try
        {
            GameDataStatus status = await gameDataService.CheckAvailabilityAsync(cancellationToken);
            ApplyGameDataStatus(status);
            eventLog.Add($"Lumina 状態: {luminaItem.Status}");
        }
        catch (OperationCanceledException)
        {
            // キャンセル時は状態変更を行わない。
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Lumina の状態確認に失敗しました。");
            luminaItem.Status = "エラー";
            luminaItem.StatusTone = "error";
            luminaItem.Detail = "Lumina の初期化確認でエラーが発生しました。";
            eventLog.Add("Lumina の初期化確認に失敗しました。", "Error");
        }
    }

    private async Task ConnectOverlayPluginAsync(CancellationToken cancellationToken = default)
    {
        overlayItem.Status = "接続中...";
        overlayItem.StatusTone = "neutral";
        overlayItem.Detail = "OverlayPlugin WebSocket に接続しています。";
        ConnectOverlayPluginCommand.NotifyCanExecuteChanged();

        try
        {
            await overlayPluginConnectionStateService.ConnectAsync(cancellationToken);
            ApplyOverlayPluginStatus();
            eventLog.Add($"OverlayPlugin 状態: {overlayItem.Status}");
        }
        catch (OperationCanceledException)
        {
            ApplyOverlayPluginStatus();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OverlayPlugin WebSocket への接続に失敗しました。");
            ApplyOverlayPluginStatus();
            eventLog.Add("OverlayPlugin WebSocket への接続に失敗しました。", "Error");
        }
    }

    private void ApplyGameDataStatus(GameDataStatus status)
    {
        switch (status.State)
        {
            case GameDataAvailabilityState.Unconfigured:
                luminaItem.Status = "未設定";
                luminaItem.StatusTone = "warning";
                luminaItem.Detail = "FF14 の sqpack フォルダーがまだ設定されていません。";
                break;
            case GameDataAvailabilityState.PathNotFound:
                luminaItem.Status = "パス不明";
                luminaItem.StatusTone = "error";
                luminaItem.Detail = $"sqpack フォルダーが見つかりません: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.Ready:
                luminaItem.Status = "利用可能";
                luminaItem.StatusTone = "success";
                luminaItem.Detail = $"sqpack: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.InitializationFailed:
                luminaItem.Status = "エラー";
                luminaItem.StatusTone = "error";
                luminaItem.Detail = status.ErrorMessage ?? "Lumina の初期化に失敗しました。";
                break;
            default:
                luminaItem.Status = "不明";
                luminaItem.StatusTone = "neutral";
                luminaItem.Detail = "Lumina の状態を判定できませんでした。";
                break;
        }

        RefreshLuminaCommand.NotifyCanExecuteChanged();
    }

    private void ApplyOverlayPluginStatus()
    {
        overlayItem.Status = overlayPluginConnectionStateService.State switch
        {
            OverlayPluginConnectionState.Connecting => "接続中...",
            OverlayPluginConnectionState.Connected => "接続済み",
            OverlayPluginConnectionState.Unavailable => "利用不可",
            OverlayPluginConnectionState.Error => "エラー",
            _ => "未接続",
        };

        overlayItem.StatusTone = overlayPluginConnectionStateService.State switch
        {
            OverlayPluginConnectionState.Connected => "success",
            OverlayPluginConnectionState.Connecting => "neutral",
            OverlayPluginConnectionState.Unavailable => "error",
            OverlayPluginConnectionState.Error => "error",
            _ => "warning",
        };

        overlayItem.Detail = overlayPluginConnectionStateService.Message;
        ConnectOverlayPluginCommand.NotifyCanExecuteChanged();
    }

    private void OnOverlayPluginStateChanged(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            ApplyOverlayPluginStatus();
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(ApplyOverlayPluginStatus);
    }

    private bool CanRefreshLumina()
    {
        return luminaItem.Status != "利用可能";
    }

    private bool CanConnectOverlayPlugin()
    {
        return overlayPluginConnectionStateService.State is not OverlayPluginConnectionState.Connected
            and not OverlayPluginConnectionState.Connecting;
    }
}
