using System.Collections.ObjectModel;
using XivLinker.Infrastructure.Overlay.Models;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public sealed class OverlayWebSocketLogViewModel
{
    private const int MaxItems = 800;

    public OverlayWebSocketLogViewModel(IOverlayPluginWebSocketSessionService sessionService)
    {
        Items = [];
        sessionService.CommunicationLogged += OnCommunicationLogged;
    }

    public ObservableCollection<OverlayWebSocketLogItemViewModel> Items { get; }

    public void Clear()
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            Items.Clear();
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(Items.Clear);
    }

    private void OnCommunicationLogged(object? sender, OverlayWebSocketCommunicationLogEntry entry)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            AddEntry(entry);
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => AddEntry(entry));
    }

    private void AddEntry(OverlayWebSocketCommunicationLogEntry entry)
    {
        Items.Insert(0, new OverlayWebSocketLogItemViewModel(entry));

        while (Items.Count > MaxItems)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }
}
