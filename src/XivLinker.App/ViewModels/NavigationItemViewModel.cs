using CommunityToolkit.Mvvm.ComponentModel;

namespace XivLinker.App.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(
        string key,
        string title,
        string icon,
        object contentViewModel)
    {
        Key = key;
        Title = title;
        Icon = icon;
        ContentViewModel = contentViewModel;
    }

    public string Key
    {
        get;
    }

    public string Title
    {
        get;
    }

    public string Icon
    {
        get;
    }

    public object ContentViewModel
    {
        get;
    }

    [ObservableProperty]
    private bool isSelected;
}
