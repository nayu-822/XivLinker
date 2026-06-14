using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public sealed class AutoCraftSequenceListViewModel
{
    private readonly ICraftSequenceStore craftSequenceStore;
    private readonly Action<CraftSequence?> openEditor;
    private readonly Action<Guid> deleteSequence;

    public AutoCraftSequenceListViewModel(
        ICraftSequenceStore craftSequenceStore,
        Action<CraftSequence?> openEditor,
        Action<Guid> deleteSequence)
    {
        this.craftSequenceStore = craftSequenceStore;
        this.openEditor = openEditor;
        this.deleteSequence = deleteSequence;

        Sequences = new ObservableCollection<CraftSequenceSummaryViewModel>();
        CreateSequenceCommand = new RelayCommand(CreateSequence);
        EditSequenceCommand = new RelayCommand<CraftSequenceSummaryViewModel>(EditSequence);
        DeleteSequenceCommand = new RelayCommand<CraftSequenceSummaryViewModel>(DeleteSequence);
    }

    public ObservableCollection<CraftSequenceSummaryViewModel> Sequences
    {
        get;
    }

    public IRelayCommand CreateSequenceCommand
    {
        get;
    }

    public IRelayCommand<CraftSequenceSummaryViewModel> EditSequenceCommand
    {
        get;
    }

    public IRelayCommand<CraftSequenceSummaryViewModel> DeleteSequenceCommand
    {
        get;
    }

    public string EmptyMessage => "まだクラフトシーケンスは登録されていません。";

    public void Refresh()
    {
        Sequences.Clear();

        foreach (CraftSequence sequence in craftSequenceStore.GetAll())
        {
            Sequences.Add(CraftSequenceSummaryViewModel.FromModel(sequence));
        }
    }

    public void Save(CraftSequence sequence)
    {
        craftSequenceStore.Save(sequence);
        Refresh();
    }

    public void Delete(Guid sequenceId)
    {
        deleteSequence(sequenceId);
        Refresh();
    }

    private void CreateSequence()
    {
        openEditor(null);
    }

    private void EditSequence(CraftSequenceSummaryViewModel? sequence)
    {
        if (sequence is null)
        {
            return;
        }

        openEditor(craftSequenceStore.Find(sequence.SequenceId));
    }

    private void DeleteSequence(CraftSequenceSummaryViewModel? sequence)
    {
        if (sequence is null)
        {
            return;
        }

        Delete(sequence.SequenceId);
    }
}
