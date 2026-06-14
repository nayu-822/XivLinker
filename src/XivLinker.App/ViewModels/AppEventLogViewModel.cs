using System.Collections.ObjectModel;

namespace XivLinker.App.ViewModels;

public sealed class AppEventLogViewModel
{
    public AppEventLogViewModel()
    {
        Items = new ObservableCollection<string>
        {
            "XivLinker \u3092\u8D77\u52D5\u3057\u307E\u3057\u305F\u3002",
        };
    }

    public ObservableCollection<string> Items { get; }

    public void Add(string message)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            Items.Insert(0, message);
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Items.Insert(0, message));
    }
}
