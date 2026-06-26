using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.App.ViewModels;
using XivLinker.App.Views;
using XivLinker.Domain.Models;

namespace XivLinker.App.Services;

public sealed class AutoCraftSequenceEditorDialogService : IAutoCraftSequenceEditorDialogService
{
    private readonly ICrafterActionCatalogService crafterActionCatalogService;
    private readonly CraftActionIconSourceService craftActionIconSourceService;
    private readonly ILogger<AutoCraftSequenceEditorDialogService> logger;
    private AutoCraftSequenceEditorWindow? currentWindow;

    public AutoCraftSequenceEditorDialogService(
        ICrafterActionCatalogService crafterActionCatalogService,
        CraftActionIconSourceService craftActionIconSourceService,
        ILogger<AutoCraftSequenceEditorDialogService> logger)
    {
        this.crafterActionCatalogService = crafterActionCatalogService;
        this.craftActionIconSourceService = craftActionIconSourceService;
        this.logger = logger;
    }

    public async Task ShowEditorAsync(CraftSequence? sequence, Action<CraftSequence> save)
    {
        if (currentWindow is not null)
        {
            currentWindow.Activate();
            return;
        }

        var closeWithoutConfirmation = false;
        AutoCraftSequenceEditorWindow? window = null;

        var viewModel = new AutoCraftSequenceEditorViewModel(
            crafterActionCatalogService,
            craftActionIconSourceService,
            () => window?.Close(),
            savedSequence =>
            {
                save(savedSequence);
                closeWithoutConfirmation = true;
                window?.Close();
            });

        window = new AutoCraftSequenceEditorWindow
        {
            DataContext = viewModel,
            Owner = ResolveOwnerWindow(),
        };

        window.Closing += (_, eventArgs) =>
        {
            if (closeWithoutConfirmation || !viewModel.HasUnsavedChanges)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                window,
                "保存していない変更があります。破棄して閉じますか？",
                "自動クラフト",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                eventArgs.Cancel = true;
            }
        };

        window.Closed += (_, _) => currentWindow = null;
        currentWindow = window;

        try
        {
            await viewModel.LoadAsync(sequence);
            window.ShowDialog();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "AutoCraft sequence editor dialog failed to open.");

            MessageBox.Show(
                ResolveOwnerWindow(),
                "クラフトシーケンス編集ダイアログの表示中にエラーが発生しました。アプリは継続できます。",
                "自動クラフト",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            if (ReferenceEquals(currentWindow, window))
            {
                currentWindow = null;
            }
        }
    }

    private static Window? ResolveOwnerWindow()
    {
        Window? activeWindow = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(static window => window.IsActive);

        return activeWindow ?? System.Windows.Application.Current.MainWindow;
    }
}
