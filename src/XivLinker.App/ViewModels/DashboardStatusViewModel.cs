using CommunityToolkit.Mvvm.ComponentModel;
using XivLinker.Infrastructure.Overlay.Models;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public partial class DashboardStatusViewModel : ObservableObject
{
    private readonly IOverlayPluginCurrentPlayerStateService currentPlayerStateService;
    private readonly AppEventLogViewModel eventLog;
    private string? lastMapName;
    private string? lastJobName;
    private string? lastIssueMessage;

    public DashboardStatusViewModel(
        IOverlayPluginCurrentPlayerStateService currentPlayerStateService,
        AppEventLogViewModel eventLog)
    {
        this.currentPlayerStateService = currentPlayerStateService;
        this.eventLog = eventLog;
        currentPlayerStateService.StateChanged += OnCurrentPlayerStateChanged;
        ApplyState(currentPlayerStateService.CurrentState, logChanges: false);
    }

    [ObservableProperty]
    private string currentMap = "接続待ち";

    [ObservableProperty]
    private string currentCoordinates = "未取得";

    [ObservableProperty]
    private string currentJob = "未取得";

    private void OnCurrentPlayerStateChanged(object? sender, EventArgs e)
    {
        CurrentPlayerState state = currentPlayerStateService.CurrentState;

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            ApplyState(state, logChanges: true);
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ApplyState(state, logChanges: true));
    }

    private void ApplyState(CurrentPlayerState state, bool logChanges)
    {
        CurrentMap = string.IsNullOrWhiteSpace(state.MapName) ? "未取得" : state.MapName;
        CurrentCoordinates = string.IsNullOrWhiteSpace(state.CoordinatesText) ? "未取得" : state.CoordinatesText;
        CurrentJob = string.IsNullOrWhiteSpace(state.ClassJobName) ? "未取得" : state.ClassJobName;

        if (!logChanges)
        {
            lastMapName = CurrentMap;
            lastJobName = CurrentJob;
            lastIssueMessage = state.IssueMessage;
            return;
        }

        if (string.Equals(CurrentMap, "現在状態を取得中", StringComparison.Ordinal)
            && !string.Equals(lastMapName, CurrentMap, StringComparison.Ordinal))
        {
            eventLog.Add("OverlayPlugin の現在状態購読を開始しました。");
        }

        if (!string.Equals(lastMapName, CurrentMap, StringComparison.Ordinal))
        {
            eventLog.Add($"現在マップを更新しました: {CurrentMap}");
            lastMapName = CurrentMap;
        }

        if (!string.Equals(lastJobName, CurrentJob, StringComparison.Ordinal))
        {
            eventLog.Add($"現在ジョブを更新しました: {CurrentJob}");
            lastJobName = CurrentJob;
        }

        if (!string.IsNullOrWhiteSpace(state.IssueMessage)
            && !string.Equals(lastIssueMessage, state.IssueMessage, StringComparison.Ordinal))
        {
            eventLog.Add(state.IssueMessage, "Warning");
        }

        lastIssueMessage = state.IssueMessage;
    }
}
