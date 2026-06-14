using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DataSourceStatusViewModel dataSourceStatusViewModel;

    [ObservableProperty]
    private string currentPageTitle = "\u30C0\u30C3\u30B7\u30E5\u30DC\u30FC\u30C9";

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
            new("dashboard", "\u30C0\u30C3\u30B7\u30E5\u30DC\u30FC\u30C9", "\u25A3", dashboardViewModel),
            new("auto-craft", "\u81EA\u52D5\u30AF\u30E9\u30D5\u30C8", "\u2692", autoCraftViewModel),
        };

        SelectedNavigationItem = NavigationItems[0];
        this.dataSourceStatusViewModel.PropertyChanged += OnDataSourceStatusPropertyChanged;
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public IRelayCommand<NavigationItemViewModel> SelectNavigationItemCommand { get; }

    public string ShellStatusText => dataSourceStatusViewModel.ShellStatusText;

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

    private void OnDataSourceStatusPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataSourceStatusViewModel.ShellStatusText))
        {
            OnPropertyChanged(nameof(ShellStatusText));
        }
    }
}
