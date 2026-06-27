using Microsoft.Extensions.Logging;
using XivLinker.App.ViewModels;
using XivLinker.Domain.Models;

namespace XivLinker.App.Services;

public sealed class AutoCraftExecutionService
{
    private readonly OverlayWindowService overlayWindowService;
    private readonly AppEventLogViewModel eventLog;
    private readonly ILogger<AutoCraftExecutionService> logger;
    private CancellationTokenSource? executionCts;
    private Task? currentExecutionTask;

    public AutoCraftExecutionService(
        OverlayWindowService overlayWindowService,
        AppEventLogViewModel eventLog,
        ILogger<AutoCraftExecutionService> logger)
    {
        this.overlayWindowService = overlayWindowService;
        this.eventLog = eventLog;
        this.logger = logger;
    }

    public event EventHandler? StateChanged;

    public bool IsRunning { get; private set; }

    public string CurrentSequenceName { get; private set; } = string.Empty;

    public async Task<bool> StartAsync(CraftSequence sequence, int runCount, CancellationToken cancellationToken = default)
    {
        if (IsRunning || runCount < 1)
        {
            return false;
        }

        var overlayViewModel = new AutoCraftRunOverlayViewModel(sequence.Name, CancelAsync);

        try
        {
            executionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;
            CurrentSequenceName = sequence.Name;
            RaiseStateChanged();

            overlayWindowService.ShowRunOverlay(overlayViewModel);
            overlayWindowService.HideMainWindow();
            eventLog.Add($"自動クラフトを開始しました: {sequence.Name} / {runCount} 回");

            currentExecutionTask = ExecuteAsync(sequence, runCount, executionCts.Token);
            _ = currentExecutionTask;
            await Task.CompletedTask;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to start auto craft execution.");
            eventLog.Add("自動クラフトの開始に失敗しました。", "Error");
            await RestoreWindowsAsync();
            ResetState();
            return false;
        }
    }

    public Task CancelAsync()
    {
        if (!IsRunning)
        {
            return Task.CompletedTask;
        }

        executionCts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(CraftSequence sequence, int runCount, CancellationToken cancellationToken)
    {
        try
        {
            for (int index = 0; index < runCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                eventLog.Add($"自動クラフト実行 {index + 1}/{runCount}: {sequence.Name}");
                await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken);
            }

            eventLog.Add($"自動クラフトが完了しました: {sequence.Name}");
        }
        catch (OperationCanceledException)
        {
            eventLog.Add($"自動クラフトを停止しました: {sequence.Name}", "Warning");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Auto craft execution failed.");
            eventLog.Add($"自動クラフト実行中にエラーが発生しました: {sequence.Name}", "Error");
        }
        finally
        {
            await RestoreWindowsAsync();
            ResetState();
        }
    }

    private async Task RestoreWindowsAsync()
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            overlayWindowService.CloseRunOverlay();
            overlayWindowService.ShowMainWindow();
            return;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            overlayWindowService.CloseRunOverlay();
            overlayWindowService.ShowMainWindow();
        });
    }

    private void ResetState()
    {
        executionCts?.Dispose();
        executionCts = null;
        currentExecutionTask = null;
        CurrentSequenceName = string.Empty;
        IsRunning = false;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }
}
