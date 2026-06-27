namespace XivLinker.App.ViewModels;

public sealed class DashboardViewModel
{
    public DashboardViewModel(
        DataSourceStatusViewModel dataSourceStatus,
        DashboardStatusViewModel status)
    {
        DataSourceStatus = dataSourceStatus;
        Status = status;
    }

    public DataSourceStatusViewModel DataSourceStatus
    {
        get;
    }

    public DashboardStatusViewModel Status
    {
        get;
    }
}
