using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public sealed class AutoCraftRunOverlayViewModel : ObservableObject
{
    private string statusText = "自動クラフトを準備しています。";

    public AutoCraftRunOverlayViewModel(
        string sequenceName,
        Func<Task> stop)
    {
        SequenceName = sequenceName;
        StopCommand = new AsyncRelayCommand(stop);
    }

    public string SequenceName { get; }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public IAsyncRelayCommand StopCommand { get; }
}
