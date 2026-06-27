using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public partial class DataSourceStatusItemViewModel : ObservableObject
{
    public DataSourceStatusItemViewModel(
        string name,
        string status,
        string detail,
        string statusTone = "neutral",
        string? actionLabel = null,
        IAsyncRelayCommand? actionCommand = null)
    {
        Name = name;
        this.status = status;
        this.detail = detail;
        this.statusTone = statusTone;
        ActionLabel = actionLabel;
        ActionCommand = actionCommand;
    }

    public string Name
    {
        get;
    }

    [ObservableProperty]
    private string status;

    [ObservableProperty]
    private string detail;

    [ObservableProperty]
    private string statusTone;

    public string? ActionLabel
    {
        get;
    }

    public IAsyncRelayCommand? ActionCommand
    {
        get;
    }

    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionLabel);
}
