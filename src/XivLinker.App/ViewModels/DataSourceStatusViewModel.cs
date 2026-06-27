using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using XivLinker.Infrastructure.CharacterConfig.Models;
using XivLinker.Infrastructure.CharacterConfig.Services;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public partial class DataSourceStatusViewModel : ObservableObject
{
    private readonly IGameDataService gameDataService;
    private readonly OverlayPluginConnectionStateService overlayPluginConnectionStateService;
    private readonly ICharacterProfileStore characterProfileStore;
    private readonly AppEventLogViewModel eventLog;
    private readonly ILogger<DataSourceStatusViewModel> logger;
    private readonly DataSourceStatusItemViewModel characterConfigItem;
    private readonly DataSourceStatusItemViewModel luminaItem;
    private readonly DataSourceStatusItemViewModel overlayItem;

    public DataSourceStatusViewModel(
        IGameDataService gameDataService,
        OverlayPluginConnectionStateService overlayPluginConnectionStateService,
        ICharacterProfileStore characterProfileStore,
        AppEventLogViewModel eventLog,
        ILogger<DataSourceStatusViewModel> logger)
    {
        this.gameDataService = gameDataService;
        this.overlayPluginConnectionStateService = overlayPluginConnectionStateService;
        this.characterProfileStore = characterProfileStore;
        this.eventLog = eventLog;
        this.logger = logger;

        RefreshLuminaCommand = new AsyncRelayCommand(RefreshGameDataStatusAsync, CanRefreshLumina);
        ConnectOverlayPluginCommand = new AsyncRelayCommand(ConnectOverlayPluginAsync, CanConnectOverlayPlugin);

        overlayItem = new DataSourceStatusItemViewModel(
            "OverlayPlugin WebSocket",
            "未接続",
            "接続先 URI は設定画面で確認できます。",
            "warning",
            "接続する",
            ConnectOverlayPluginCommand);

        luminaItem = new DataSourceStatusItemViewModel(
            "Lumina",
            "未設定",
            "sqpack パスは設定画面で確認できます。",
            "warning",
            "状態を確認",
            RefreshLuminaCommand);

        characterConfigItem = new DataSourceStatusItemViewModel(
            "キャラクター設定",
            "未設定",
            "キャラクター設定フォルダーを追加して読み込みできます。",
            "warning");

        Items = new ObservableCollection<DataSourceStatusItemViewModel>
        {
            overlayItem,
            luminaItem,
            characterConfigItem,
            new(
                "ログ",
                "利用可能",
                "ログ画面で確認できます。",
                "success"),
        };

        this.overlayPluginConnectionStateService.StateChanged += OnOverlayPluginStateChanged;
        this.characterProfileStore.StateChanged += OnCharacterProfileStoreStateChanged;
        UpdateCharacterConfigStatus();
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
        luminaItem.SettingsDetail = "sqpack パスを確認しています。";

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
            luminaItem.SettingsDetail = "設定画面で sqpack パスを確認してから再試行してください。";
            eventLog.Add("Lumina の状態確認に失敗しました。", "Error");
        }
    }

    private async Task ConnectOverlayPluginAsync(CancellationToken cancellationToken = default)
    {
        overlayItem.Status = "接続中...";
        overlayItem.StatusTone = "neutral";
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
                luminaItem.SettingsDetail = "sqpack パスが未設定です。設定画面で確認してください。";
                break;
            case GameDataAvailabilityState.PathNotFound:
                luminaItem.Status = "利用不可";
                luminaItem.StatusTone = "error";
                luminaItem.SettingsDetail = $"設定済みの sqpack パスを確認してください: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.Ready:
                luminaItem.Status = "利用可能";
                luminaItem.StatusTone = "success";
                luminaItem.SettingsDetail = $"設定済みの sqpack パス: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.InitializationFailed:
                luminaItem.Status = "エラー";
                luminaItem.StatusTone = "error";
                luminaItem.SettingsDetail = status.ErrorMessage ?? "sqpack パスと Lumina の初期化状態を確認してください。";
                break;
            default:
                luminaItem.Status = "不明";
                luminaItem.StatusTone = "neutral";
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

        overlayItem.SettingsDetail = overlayPluginConnectionStateService.Message;
        ConnectOverlayPluginCommand.NotifyCanExecuteChanged();
    }

    private void UpdateCharacterConfigStatus()
    {
        IReadOnlyList<CharacterProfile> profiles = characterProfileStore.Profiles;
        CharacterProfile? selectedProfile = characterProfileStore.SelectedProfile;
        CharacterData? selectedData = characterProfileStore.SelectedCharacterData;

        if (profiles.Count == 0)
        {
            characterConfigItem.Status = "未設定";
            characterConfigItem.StatusTone = "warning";
            characterConfigItem.SettingsDetail = "キャラクター設定フォルダーを追加してください。";
            return;
        }

        if (selectedProfile is null)
        {
            characterConfigItem.Status = "未選択";
            characterConfigItem.StatusTone = "warning";
            characterConfigItem.SettingsDetail = "設定画面で対象キャラクターを選択してください。";
            return;
        }

        if (selectedData is null)
        {
            characterConfigItem.Status = "未読み込み";
            characterConfigItem.StatusTone = "neutral";
            characterConfigItem.SettingsDetail = $"{selectedProfile.DisplayName} の設定読み込みを実行してください。";
            return;
        }

        if (selectedData.Errors.Count > 0)
        {
            characterConfigItem.Status = "エラー";
            characterConfigItem.StatusTone = "error";
            characterConfigItem.SettingsDetail = string.Join(Environment.NewLine, selectedData.Errors);
            return;
        }

        characterConfigItem.Status = "読み込み済み";
        characterConfigItem.StatusTone = "success";
        characterConfigItem.SettingsDetail = $"{selectedProfile.DisplayName} の HOTBAR.DAT / KEYBIND.DAT を読み込み済みです。";
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

    private void OnCharacterProfileStoreStateChanged(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            UpdateCharacterConfigStatus();
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(UpdateCharacterConfigStatus);
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
