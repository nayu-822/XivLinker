using CommunityToolkit.Mvvm.ComponentModel;
using XivLinker.Application.Abstractions;
using XivLinker.App.Services;
using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public partial class AutoCraftViewModel : ObservableObject
{
    public AutoCraftViewModel(
        ICraftSequenceStore craftSequenceStore,
        IAutoCraftSequenceEditorDialogService sequenceEditorDialogService,
        OverlayWindowService overlayWindowService,
        AutoCraftExecutionService autoCraftExecutionService)
    {
        SequenceList = new AutoCraftSequenceListViewModel(
            craftSequenceStore,
            sequence => sequenceEditorDialogService.ShowEditorAsync(sequence, SaveSequence),
            overlayWindowService,
            autoCraftExecutionService);
        SequenceList.Refresh();
    }

    public AutoCraftSequenceListViewModel SequenceList
    {
        get;
    }

    public string Title => "自動クラフト";

    public string Description => "保存済みシーケンスの確認、作成、編集、削除、実行をここから行えます。";

    private void SaveSequence(CraftSequence sequence)
    {
        SequenceList.Save(sequence);
    }
}
