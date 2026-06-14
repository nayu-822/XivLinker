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
    }

    public DataSourceStatusViewModel DataSourceStatus { get; }

    public DashboardStatusViewModel Status { get; }

    public AppEventLogViewModel EventLog { get; }
}
