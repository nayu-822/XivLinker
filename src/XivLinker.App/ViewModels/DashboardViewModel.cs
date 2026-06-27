using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace XivLinker.App.ViewModels;

public sealed class DashboardViewModel
{
    public DashboardViewModel(
        DataSourceStatusViewModel dataSourceStatus,
        DashboardStatusViewModel status,
        AppEventLogViewModel eventLog)
    {
        DataSourceStatus = dataSourceStatus;
        Status = status;
        EventLog = eventLog;
        RecentEventLogItems = new ObservableCollection<AppEventLogItemViewModel>();
        EventLog.Items.CollectionChanged += OnEventLogItemsChanged;
        RefreshRecentItems();
    }

    public DataSourceStatusViewModel DataSourceStatus
    {
        get;
    }

    public DashboardStatusViewModel Status
    {
        get;
    }

    public AppEventLogViewModel EventLog
    {
        get;
    }

    public ObservableCollection<AppEventLogItemViewModel> RecentEventLogItems
    {
        get;
    }

    private void OnEventLogItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRecentItems();
    }

    private void RefreshRecentItems()
    {
        RecentEventLogItems.Clear();

        foreach (AppEventLogItemViewModel item in EventLog.Items.Take(3))
        {
            RecentEventLogItems.Add(item);
        }
    }
}
