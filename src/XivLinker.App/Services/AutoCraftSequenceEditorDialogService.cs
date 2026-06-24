using System.Linq;
using System.Windows;
using XivLinker.Application.Abstractions;
using XivLinker.App.ViewModels;
using XivLinker.App.Views;
using XivLinker.Domain.Models;

namespace XivLinker.App.Services;

public sealed class AutoCraftSequenceEditorDialogService : IAutoCraftSequenceEditorDialogService
{
    private readonly ICrafterActionCatalogService crafterActionCatalogService;
    private readonly CraftActionIconSourceService craftActionIconSourceService;
    private AutoCraftSequenceEditorWindow? currentWindow;

    public AutoCraftSequenceEditorDialogService(
        ICrafterActionCatalogService crafterActionCatalogService,
        CraftActionIconSourceService craftActionIconSourceService)
    {
        this.crafterActionCatalogService = crafterActionCatalogService;
        this.craftActionIconSourceService = craftActionIconSourceService;
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

        await viewModel.LoadAsync(sequence);
        window.ShowDialog();
    }

    private static Window? ResolveOwnerWindow()
    {
        Window? activeWindow = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(static window => window.IsActive);

        return activeWindow ?? System.Windows.Application.Current.MainWindow;
    }
}
