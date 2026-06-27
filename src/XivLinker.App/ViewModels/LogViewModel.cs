using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public sealed class LogViewModel
{
    private readonly AppEventLogViewModel eventLog;

    public LogViewModel(AppEventLogViewModel eventLog)
    {
        this.eventLog = eventLog;
        ClearLogCommand = new RelayCommand(ClearLog, CanClearLog);

        // These page view models are singletons today. If their lifetime changes,
        // move command-state updates into a shared log presenter/service.
        eventLog.Items.CollectionChanged += OnItemsChanged;
    }

    public AppEventLogViewModel EventLog => eventLog;

    public IRelayCommand ClearLogCommand
    {
        get;
    }

    private bool CanClearLog()
    {
        return eventLog.Items.Count > 0;
    }

    private void ClearLog()
    {
        eventLog.Clear();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearLogCommand.NotifyCanExecuteChanged();
    }
}
