using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel(
        IOptions<OverlayPluginOptions> overlayPluginOptions,
        IOptions<LuminaOptions> luminaOptions,
        DataSourceStatusViewModel dataSourceStatus,
        AppEventLogViewModel eventLog)
    {
        OverlayWebSocketUri = overlayPluginOptions.Value.WebSocketUri;
        SqPackPath = string.IsNullOrWhiteSpace(luminaOptions.Value.SqPackPath)
            ? "未設定"
            : luminaOptions.Value.SqPackPath!;
        DataSourceStatus = dataSourceStatus;
        EventLog = eventLog;
        ReconnectOverlayCommand = dataSourceStatus.ConnectOverlayPluginCommand;
        RefreshLuminaCommand = dataSourceStatus.RefreshLuminaCommand;
        ClearLogCommand = new RelayCommand(ClearLog, CanClearLog);

        // These page view models are singletons today. If their lifetime changes,
        // move command-state updates into a shared log presenter/service.
        EventLog.Items.CollectionChanged += OnItemsChanged;
    }

    public DataSourceStatusViewModel DataSourceStatus
    {
        get;
    }

    public AppEventLogViewModel EventLog
    {
        get;
    }

    public string OverlayWebSocketUri
    {
        get;
    }

    public string SqPackPath
    {
        get;
    }

    public string CharacterConfigStatus => "未実装";

    public string CharacterConfigDescription => "キャラクター設定ファイルの参照先指定は今後対応予定です。";

    public string LuminaPathDescription => "パスの変更や参照ダイアログは今後対応予定です。";

    public string LogRetentionDescription => "未実装: 現在はメモリ上のログをそのまま表示しています。";

    public IAsyncRelayCommand ReconnectOverlayCommand
    {
        get;
    }

    public IAsyncRelayCommand RefreshLuminaCommand
    {
        get;
    }

    public IRelayCommand ClearLogCommand
    {
        get;
    }

    private bool CanClearLog()
    {
        return EventLog.Items.Count > 0;
    }

    private void ClearLog()
    {
        EventLog.Clear();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearLogCommand.NotifyCanExecuteChanged();
    }
}
