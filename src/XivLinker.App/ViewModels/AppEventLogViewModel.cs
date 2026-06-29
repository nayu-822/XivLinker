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
            AddCore(item);
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            AddCore(item);
            return;
        }

        _ = application.Dispatcher.InvokeAsync(() => AddCore(item));
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

    private void AddCore(AppEventLogItemViewModel item)
    {
        if (Items.Count > 0
            && string.Equals(Items[0].Level, item.Level, StringComparison.Ordinal)
            && string.Equals(Items[0].Message, item.Message, StringComparison.Ordinal))
        {
            return;
        }

        Items.Insert(0, item);
    }
}
