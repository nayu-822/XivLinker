using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public partial class DataSourceStatusItemViewModel : ObservableObject
{
    public DataSourceStatusItemViewModel(
        string name,
        string status,
        string settingsDetail,
        string statusTone = "neutral",
        string? actionLabel = null,
        IAsyncRelayCommand? actionCommand = null,
        string? supplementText = null)
    {
        Name = name;
        this.status = status;
        this.settingsDetail = settingsDetail;
        this.statusTone = statusTone;
        this.supplementText = supplementText ?? string.Empty;
        ActionLabel = actionLabel;
        ActionCommand = actionCommand;
    }

    public string Name { get; }

    [ObservableProperty]
    private string status;

    [ObservableProperty]
    private string settingsDetail;

    [ObservableProperty]
    private string statusTone;

    [ObservableProperty]
    private string supplementText;

    public string? ActionLabel { get; }

    public IAsyncRelayCommand? ActionCommand { get; }

    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionLabel);

    public bool HasSupplementText => !string.IsNullOrWhiteSpace(SupplementText);
}
