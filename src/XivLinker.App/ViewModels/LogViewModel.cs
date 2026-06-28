using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public partial class LogViewModel : ObservableObject
{
    private readonly AppEventLogViewModel eventLog;
    private readonly OverlayWebSocketLogViewModel webSocketLog;

    [ObservableProperty]
    private int selectedTabIndex;

    public LogViewModel(
        AppEventLogViewModel eventLog,
        OverlayWebSocketLogViewModel webSocketLog)
    {
        this.eventLog = eventLog;
        this.webSocketLog = webSocketLog;
        ClearLogCommand = new RelayCommand(ClearLog, CanClearLog);

        // These page view models are singletons today. If their lifetime changes,
        // move command-state updates into a shared log presenter/service.
        eventLog.Items.CollectionChanged += OnItemsChanged;
        webSocketLog.Items.CollectionChanged += OnItemsChanged;
    }

    public AppEventLogViewModel EventLog => eventLog;

    public OverlayWebSocketLogViewModel WebSocketLog => webSocketLog;

    public IRelayCommand ClearLogCommand
    {
        get;
    }

    private bool CanClearLog()
    {
        return SelectedTabIndex == 1
            ? webSocketLog.Items.Count > 0
            : eventLog.Items.Count > 0;
    }

    private void ClearLog()
    {
        if (SelectedTabIndex == 1)
        {
            webSocketLog.Clear();
            return;
        }

        eventLog.Clear();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearLogCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        ClearLogCommand.NotifyCanExecuteChanged();
    }
}
