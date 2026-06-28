using System.Linq;
using System.Windows;
using XivLinker.Application.Models;
using XivLinker.App.ViewModels;
using XivLinker.App.Views;

namespace XivLinker.App.Services;

public class OverlayWindowService
{
    private const string AutoCraftTitle = "自動クラフト";

    private AutoCraftRunOptionsWindow? currentRunOptionsWindow;
    private AutoCraftRunOverlayWindow? currentRunOverlayWindow;

    public virtual Task<int?> ShowRunOptionsAsync(string sequenceName)
    {
        if (currentRunOptionsWindow is not null)
        {
            currentRunOptionsWindow.Activate();
            return Task.FromResult<int?>(null);
        }

        int? confirmedRunCount = null;
        AutoCraftRunOptionsWindow? window = null;

        var viewModel = new AutoCraftRunOptionsViewModel(
            sequenceName,
            runCount =>
            {
                confirmedRunCount = runCount;
                if (window is not null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            },
            () =>
            {
                if (window is not null)
                {
                    window.DialogResult = false;
                    window.Close();
                }
            });

        window = new AutoCraftRunOptionsWindow
        {
            DataContext = viewModel,
            Owner = ResolveOwnerWindow(),
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(currentRunOptionsWindow, window))
            {
                currentRunOptionsWindow = null;
            }
        };

        currentRunOptionsWindow = window;
        bool? result = window.ShowDialog();
        return Task.FromResult(result == true ? confirmedRunCount : null);
    }

    public virtual Task ShowCraftExecutionPreparationFailedAsync(
        string sequenceName,
        string crafterJobName,
        IReadOnlyList<CraftActionRequirement> missingActions,
        IReadOnlyList<CraftActionRequirement> unboundActions)
    {
        string missingActionLines = missingActions.Count == 0
            ? "なし"
            : string.Join(Environment.NewLine, missingActions.Select(static action => $"・{action.ActionName}"));
        string unboundActionLines = unboundActions.Count == 0
            ? "なし"
            : string.Join(Environment.NewLine, unboundActions.Select(static action => $"・{action.ActionName}"));

        MessageBox.Show(
            ResolveOwnerWindow(),
            $"シーケンスを実行できません。{Environment.NewLine}{Environment.NewLine}" +
            $"シーケンス: {sequenceName}{Environment.NewLine}" +
            $"現在ジョブ: {crafterJobName}{Environment.NewLine}{Environment.NewLine}" +
            $"ホットバーに未登録のアクション:{Environment.NewLine}{missingActionLines}{Environment.NewLine}{Environment.NewLine}" +
            $"キー未割り当てのアクション:{Environment.NewLine}{unboundActionLines}",
            AutoCraftTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return Task.CompletedTask;
    }

    public virtual Task ShowMessageAsync(string title, string message)
    {
        MessageBox.Show(
            ResolveOwnerWindow(),
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        return Task.CompletedTask;
    }

    public virtual void ShowRunOverlay(AutoCraftRunOverlayViewModel viewModel)
    {
        if (currentRunOverlayWindow is not null)
        {
            currentRunOverlayWindow.DataContext = viewModel;
            return;
        }

        var window = new AutoCraftRunOverlayWindow
        {
            DataContext = viewModel,
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(currentRunOverlayWindow, window))
            {
                currentRunOverlayWindow = null;
            }
        };

        currentRunOverlayWindow = window;
        window.Show();
    }

    public virtual void CloseRunOverlay()
    {
        currentRunOverlayWindow?.Close();
        currentRunOverlayWindow = null;
    }

    public virtual void HideMainWindow()
    {
        ResolveMainWindow()?.Hide();
    }

    public virtual void ShowMainWindow()
    {
        ShowMainWindow(activate: true);
    }

    public virtual void ShowMainWindow(bool activate)
    {
        Window? mainWindow = ResolveMainWindow();
        if (mainWindow is null)
        {
            return;
        }

        mainWindow.Show();

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        if (activate)
        {
            mainWindow.Activate();
        }
    }

    private static Window? ResolveOwnerWindow()
    {
        if (System.Windows.Application.Current is null)
        {
            return null;
        }

        Window? activeWindow = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(static window => window.IsActive);

        return activeWindow ?? ResolveMainWindow();
    }

    private static Window? ResolveMainWindow()
    {
        return System.Windows.Application.Current?.MainWindow;
    }
}
