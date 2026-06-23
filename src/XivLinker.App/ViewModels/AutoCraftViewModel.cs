using CommunityToolkit.Mvvm.ComponentModel;
using XivLinker.Application.Abstractions;
using XivLinker.App.Services;
using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public partial class AutoCraftViewModel : ObservableObject
{
    private readonly AutoCraftSequenceListViewModel sequenceListViewModel;
    private readonly AutoCraftSequenceEditorViewModel sequenceEditorViewModel;

    [ObservableProperty]
    private object? currentContentViewModel;

    public AutoCraftViewModel(
        ICraftSequenceStore craftSequenceStore,
        ICrafterActionCatalogService crafterActionCatalogService,
        CraftActionIconSourceService craftActionIconSourceService)
    {
        sequenceEditorViewModel = new AutoCraftSequenceEditorViewModel(
            crafterActionCatalogService,
            craftActionIconSourceService,
            ShowSequenceList,
            SaveSequence);
        sequenceListViewModel = new AutoCraftSequenceListViewModel(
            craftSequenceStore,
            ShowSequenceEditorAsync);

        ShowSequenceList();
    }

    public string Title => "自動クラフト";

    public string Description => "クラフトシーケンスの一覧確認と新規作成画面への移動をここから行えます。";

    public bool IsSequenceListVisible => ReferenceEquals(CurrentContentViewModel, sequenceListViewModel);

    public bool IsSequenceEditorVisible => ReferenceEquals(CurrentContentViewModel, sequenceEditorViewModel);

    private void ShowSequenceList()
    {
        sequenceListViewModel.Refresh();
        CurrentContentViewModel = sequenceListViewModel;
        OnPropertyChanged(nameof(IsSequenceListVisible));
        OnPropertyChanged(nameof(IsSequenceEditorVisible));
    }

    private async Task ShowSequenceEditorAsync(CraftSequence? sequence)
    {
        CurrentContentViewModel = sequenceEditorViewModel;
        OnPropertyChanged(nameof(IsSequenceListVisible));
        OnPropertyChanged(nameof(IsSequenceEditorVisible));
        await sequenceEditorViewModel.LoadAsync(sequence);
    }

    private void SaveSequence(CraftSequence sequence)
    {
        sequenceListViewModel.Save(sequence);
        ShowSequenceList();
    }
}
