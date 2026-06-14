using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DataSourceStatusViewModel dataSourceStatusViewModel;

    [ObservableProperty]
    private string currentPageTitle = "ダッシュボード";

    [ObservableProperty]
    private object? currentContentViewModel;

    [ObservableProperty]
    private NavigationItemViewModel? selectedNavigationItem;

    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        AutoCraftViewModel autoCraftViewModel,
        DataSourceStatusViewModel dataSourceStatusViewModel)
    {
        this.dataSourceStatusViewModel = dataSourceStatusViewModel;
        SelectNavigationItemCommand = new RelayCommand<NavigationItemViewModel>(SelectNavigationItem);

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("dashboard", "ダッシュボード", "□", dashboardViewModel),
            new("auto-craft", "自動クラフト", "⚒", autoCraftViewModel),
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems
    {
        get;
    }

    public IRelayCommand<NavigationItemViewModel> SelectNavigationItemCommand
    {
        get;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return dataSourceStatusViewModel.InitializeAsync(cancellationToken);
    }

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        foreach (NavigationItemViewModel item in NavigationItems)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        CurrentContentViewModel = value?.ContentViewModel;
        CurrentPageTitle = value?.Title ?? "XivLinker";
    }

    private void SelectNavigationItem(NavigationItemViewModel? item)
    {
        if (item is null || ReferenceEquals(item, SelectedNavigationItem))
        {
            return;
        }

        SelectedNavigationItem = item;
    }
}
