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
        System.Windows.Application? application = System.Windows.Application.Current;

        if (application is null)
        {
            Items.Insert(0, item);
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            Items.Insert(0, item);
            return;
        }

        _ = application.Dispatcher.InvokeAsync(() => Items.Insert(0, item));
    }

    public void Clear()
    {
        System.Windows.Application? application = System.Windows.Application.Current;
        if (application is null)
        {
            Items.Clear();
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            Items.Clear();
            return;
        }

        _ = application.Dispatcher.InvokeAsync(Items.Clear);
    }
}
