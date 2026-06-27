using System.Collections.ObjectModel;

namespace XivLinker.App.ViewModels;

public sealed class AppEventLogViewModel
{
    public AppEventLogViewModel()
    {
        Items = new ObservableCollection<AppEventLogItemViewModel>
        {
            new("Info", "XivLinker を起動しました。"),
        };
    }

    public ObservableCollection<AppEventLogItemViewModel> Items
    {
        get;
    }

    public void Add(string message, string level = "Info")
    {
        var item = new AppEventLogItemViewModel(level, message);

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            Items.Insert(0, item);
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Items.Insert(0, item));
    }
}
