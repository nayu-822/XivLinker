using Microsoft.Extensions.Logging;
using XivLinker.App.ViewModels;

namespace XivLinker.App.Services;

public sealed class AutoCraftExecutionService
{
    private readonly OverlayWindowService overlayWindowService;
    private readonly IAutoCraftActionExecutor autoCraftActionExecutor;
    private readonly AppEventLogViewModel eventLog;
    private readonly ILogger<AutoCraftExecutionService> logger;
    private CancellationTokenSource? executionCts;
    private AutoCraftRunOverlayViewModel? currentOverlayViewModel;

    public AutoCraftExecutionService(
        OverlayWindowService overlayWindowService,
        IAutoCraftActionExecutor autoCraftActionExecutor,
        AppEventLogViewModel eventLog,
        ILogger<AutoCraftExecutionService> logger)
    {
        this.overlayWindowService = overlayWindowService;
        this.autoCraftActionExecutor = autoCraftActionExecutor;
        this.eventLog = eventLog;
        this.logger = logger;
    }

    public event EventHandler? StateChanged;

    public bool IsRunning { get; private set; }

    public string CurrentSequenceName { get; private set; } = string.Empty;

    public async Task<bool> StartAsync(AutoCraftExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (IsRunning || context.RunCount < 1)
        {
            return false;
        }

        var overlayViewModel = new AutoCraftRunOverlayViewModel(context.Sequence.Name, CancelAsync)
        {
            StatusText = "自動クラフトを開始しています...",
        };

        try
        {
            executionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;
            CurrentSequenceName = context.Sequence.Name;
            currentOverlayViewModel = overlayViewModel;
            RaiseStateChanged();

            overlayWindowService.ShowRunOverlay(overlayViewModel);
            overlayWindowService.HideMainWindow();
            eventLog.Add($"自動クラフトを開始しました: {context.Sequence.Name} / {context.RunCount} 回");

            _ = ExecuteAsync(context, executionCts.Token);
            await Task.CompletedTask;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to start auto craft execution.");
            eventLog.Add("自動クラフトの開始に失敗しました。", "Error");
            await RestoreWindowsAsync(activateMainWindow: true);
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

    private async Task ExecuteAsync(AutoCraftExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await autoCraftActionExecutor.ExecuteAsync(
                context.Sequence,
                context.RunCount,
                UpdateOverlayStatus,
                cancellationToken);

            UpdateOverlayStatus("自動クラフトが完了しました。メインウィンドウを表示します...");
            eventLog.Add($"自動クラフトが完了しました: {context.Sequence.Name}");
            await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken);
            await RestoreWindowsAsync(activateMainWindow: false);
        }
        catch (OperationCanceledException)
        {
            UpdateOverlayStatus("中断しました。メインウィンドウへ戻ります...");
            eventLog.Add($"自動クラフトを中断しました: {context.Sequence.Name}", "Warning");
            await RestoreWindowsAsync(activateMainWindow: true);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Auto craft execution failed.");
            UpdateOverlayStatus("エラーが発生しました。メインウィンドウへ戻ります...");
            eventLog.Add($"自動クラフト実行中にエラーが発生しました: {context.Sequence.Name}", "Error");
            await Task.Delay(TimeSpan.FromMilliseconds(1200));
            await RestoreWindowsAsync(activateMainWindow: true);
        }
        finally
        {
            ResetState();
        }
    }

    private void UpdateOverlayStatus(string statusText)
    {
        if (currentOverlayViewModel is null || System.Windows.Application.Current is null)
        {
            return;
        }

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            currentOverlayViewModel.StatusText = statusText;
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => currentOverlayViewModel.StatusText = statusText);
    }

    private async Task RestoreWindowsAsync(bool activateMainWindow)
    {
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            overlayWindowService.CloseRunOverlay();
            overlayWindowService.ShowMainWindow(activateMainWindow);
            return;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            overlayWindowService.CloseRunOverlay();
            overlayWindowService.ShowMainWindow(activateMainWindow);
        });
    }

    private void ResetState()
    {
        executionCts?.Dispose();
        executionCts = null;
        currentOverlayViewModel = null;
        CurrentSequenceName = string.Empty;
        IsRunning = false;
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        if (System.Windows.Application.Current is null)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }
}
