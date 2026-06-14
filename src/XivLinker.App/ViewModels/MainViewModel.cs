using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XivLinker.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string currentArea = "-";

    [ObservableProperty]
    private string currentCharacter = "-";

    public MainViewModel()
    {
        DataSources = new ObservableCollection<DataSourceStatusItemViewModel>
        {
            new("OverlayPlugin", "\u672A\u8A2D\u5B9A", "OverlayPlugin: \u672A\u8A2D\u5B9A"),
            new("Lumina", "\u672A\u8A2D\u5B9A", "Lumina: \u672A\u8A2D\u5B9A"),
            new("\u30AD\u30E3\u30E9\u30AF\u30BF\u30FC\u8A2D\u5B9A", "\u672A\u8A2D\u5B9A", "\u30AD\u30E3\u30E9\u30AF\u30BF\u30FC\u8A2D\u5B9A: \u672A\u8A2D\u5B9A"),
            new("\u30ED\u30B0", "\u672A\u8A2D\u5B9A", "\u30ED\u30B0: \u672A\u8A2D\u5B9A"),
        };

        EventLogs = new ObservableCollection<string>
        {
            "XivLinker \u3092\u8D77\u52D5\u3057\u307E\u3057\u305F\u3002",
        };
    }

    public ObservableCollection<DataSourceStatusItemViewModel> DataSources { get; }

    public ObservableCollection<string> EventLogs { get; }
}
