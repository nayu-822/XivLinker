using CommunityToolkit.Mvvm.ComponentModel;

namespace XivLinker.App.ViewModels;

public partial class DashboardStatusViewModel : ObservableObject
{
    [ObservableProperty]
    private string currentArea = "-";

    [ObservableProperty]
    private string currentCharacter = "-";
}
