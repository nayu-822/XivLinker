using System.Collections.ObjectModel;

namespace XivLinker.App.ViewModels;

public sealed class AppEventLogViewModel
{
    public AppEventLogViewModel()
    {
        Items = new ObservableCollection<string>
        {
            "XivLinker を起動しました。",
        };
    }

    public ObservableCollection<string> Items
    {
        get;
    }

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
