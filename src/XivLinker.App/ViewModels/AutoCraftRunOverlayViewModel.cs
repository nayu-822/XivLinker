using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public sealed class AutoCraftRunOverlayViewModel : ObservableObject
{
    public AutoCraftRunOverlayViewModel(
        string sequenceName,
        Func<Task> stop)
    {
        SequenceName = sequenceName;
        StopCommand = new AsyncRelayCommand(stop);
    }

    public string SequenceName { get; }

    public string StatusText => "自動クラフトを実行中です。";

    public IAsyncRelayCommand StopCommand { get; }
}
