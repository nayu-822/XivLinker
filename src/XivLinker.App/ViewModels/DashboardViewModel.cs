namespace XivLinker.App.ViewModels;

public sealed class DashboardViewModel
{
    public DashboardViewModel(MainViewModel shell)
    {
        Shell = shell;
    }

    public MainViewModel Shell { get; }
}
