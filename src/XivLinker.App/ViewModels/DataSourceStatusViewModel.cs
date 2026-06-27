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

        overlayItem = new DataSourceStatusItemViewModel(
            "OverlayPlugin WebSocket",
            "未接続",
            "OverlayPlugin WebSocket の接続状態を確認します。",
            "接続先: OverlayPlugin WebSocket URI は設定画面で確認できます。",
            "warning",
            "接続する",
            ConnectOverlayPluginCommand);

        luminaItem = new DataSourceStatusItemViewModel(
            "Lumina",
            "未設定",
            "FF14 データの参照状態を確認します。",
            "FF14 の sqpack パスは設定画面で確認できます。",
            "warning",
            "状態を確認",
            RefreshLuminaCommand);

        Items = new ObservableCollection<DataSourceStatusItemViewModel>
        {
            overlayItem,
            luminaItem,
            new(
                "キャラクター設定",
                "未実装",
                "キャラクター設定ファイル連携は未実装です。",
                "キャラクター設定ファイルの参照先指定は今後対応予定です。",
                "warning"),
            new(
                "ログ",
                "未実装",
                "ログ連携機能は未実装です。",
                "FF14 / ACT ログ連携は今後対応予定です。",
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
        luminaItem.DashboardDescription = "FF14 データの参照状態を確認しています。";
        luminaItem.SettingsDetail = "FF14 の sqpack パスは設定画面で確認できます。";

        try
        {
            GameDataStatus status = await gameDataService.CheckAvailabilityAsync(cancellationToken);
            ApplyGameDataStatus(status);
            eventLog.Add($"Lumina 状態: {luminaItem.Status}");
        }
        catch (OperationCanceledException)
        {
            // キャンセル時の状態更新は行わない。
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Lumina の状態確認に失敗しました。");
            luminaItem.Status = "エラー";
            luminaItem.StatusTone = "error";
            luminaItem.DashboardDescription = "FF14 データの参照状態確認でエラーが発生しました。";
            luminaItem.SettingsDetail = "設定画面で sqpack パスを確認してから再試行してください。";
            eventLog.Add("Lumina の状態確認に失敗しました。", "Error");
        }
    }

    private async Task ConnectOverlayPluginAsync(CancellationToken cancellationToken = default)
    {
        overlayItem.Status = "接続中...";
        overlayItem.StatusTone = "neutral";
        overlayItem.DashboardDescription = "OverlayPlugin WebSocket に接続しています。";
        overlayItem.SettingsDetail = "接続先 URI は設定画面で確認できます。";
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
                luminaItem.DashboardDescription = "FF14 データ参照の初期設定がまだ完了していません。";
                luminaItem.SettingsDetail = "sqpack パスが未設定です。設定画面で参照先を確認してください。";
                break;
            case GameDataAvailabilityState.PathNotFound:
                luminaItem.Status = "利用不可";
                luminaItem.StatusTone = "error";
                luminaItem.DashboardDescription = "FF14 データ参照に必要なパスが見つかりません。";
                luminaItem.SettingsDetail = $"設定済みの sqpack パスを確認してください: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.Ready:
                luminaItem.Status = "利用可能";
                luminaItem.StatusTone = "success";
                luminaItem.DashboardDescription = "FF14 データを参照できます。";
                luminaItem.SettingsDetail = $"設定済みの sqpack パス: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.InitializationFailed:
                luminaItem.Status = "エラー";
                luminaItem.StatusTone = "error";
                luminaItem.DashboardDescription = "FF14 データ参照の初期化に失敗しました。";
                luminaItem.SettingsDetail = status.ErrorMessage ?? "sqpack パスと Lumina の初期化状態を確認してください。";
                break;
            default:
                luminaItem.Status = "不明";
                luminaItem.StatusTone = "neutral";
                luminaItem.DashboardDescription = "FF14 データ参照の状態を判定できませんでした。";
                luminaItem.SettingsDetail = "設定画面から状態確認を再実行してください。";
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

        overlayItem.DashboardDescription = overlayPluginConnectionStateService.State switch
        {
            OverlayPluginConnectionState.Connected => "OverlayPlugin WebSocket に接続されています。",
            OverlayPluginConnectionState.Connecting => "OverlayPlugin WebSocket に接続しています。",
            OverlayPluginConnectionState.Unavailable => "OverlayPlugin WebSocket に接続できません。",
            OverlayPluginConnectionState.Error => "OverlayPlugin WebSocket 接続でエラーが発生しました。",
            _ => "OverlayPlugin WebSocket に接続していません。",
        };

        overlayItem.SettingsDetail = overlayPluginConnectionStateService.Message;
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
