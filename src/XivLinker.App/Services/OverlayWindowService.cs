using System.Linq;
using System.Windows;
using XivLinker.Application.Models;
using XivLinker.App.ViewModels;
using XivLinker.App.Views;

namespace XivLinker.App.Services;

public class OverlayWindowService
{
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

    public virtual Task ShowMissingHotbarActionsAsync(
        string sequenceName,
        string crafterJobName,
        IReadOnlyList<CraftActionRequirement> missingActions)
    {
        string actionLines = string.Join(
            Environment.NewLine,
            missingActions.Select(static action => $"・{action.ActionName}"));

        MessageBox.Show(
            ResolveOwnerWindow(),
            $"未登録アクションがあるため、シーケンスを実行できません。{Environment.NewLine}{Environment.NewLine}" +
            $"シーケンス: {sequenceName}{Environment.NewLine}" +
            $"現在ジョブ: {crafterJobName}{Environment.NewLine}{Environment.NewLine}" +
            $"以下のアクションを現在ジョブのホットバーに登録してください。{Environment.NewLine}" +
            $"{actionLines}",
            "自動クラフト",
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

    public void ShowRunOverlay(AutoCraftRunOverlayViewModel viewModel)
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

    public void CloseRunOverlay()
    {
        currentRunOverlayWindow?.Close();
        currentRunOverlayWindow = null;
    }

    public void HideMainWindow()
    {
        ResolveMainWindow()?.Hide();
    }

    public void ShowMainWindow()
    {
        ShowMainWindow(activate: true);
    }

    public void ShowMainWindow(bool activate)
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
        Window? activeWindow = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(static window => window.IsActive);

        return activeWindow ?? ResolveMainWindow();
    }

    private static Window? ResolveMainWindow()
    {
        return System.Windows.Application.Current.MainWindow;
    }
}
