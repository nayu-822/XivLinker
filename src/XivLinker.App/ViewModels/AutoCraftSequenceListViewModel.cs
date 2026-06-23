using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public sealed class AutoCraftSequenceListViewModel
{
    private readonly ICraftSequenceStore craftSequenceStore;
    private readonly Func<CraftSequence?, Task> openEditor;

    public AutoCraftSequenceListViewModel(
        ICraftSequenceStore craftSequenceStore,
        Func<CraftSequence?, Task> openEditor)
    {
        this.craftSequenceStore = craftSequenceStore;
        this.openEditor = openEditor;

        Sequences = new ObservableCollection<CraftSequenceSummaryViewModel>();
        CreateSequenceCommand = new AsyncRelayCommand(CreateSequenceAsync);
        EditSequenceCommand = new AsyncRelayCommand<CraftSequenceSummaryViewModel>(EditSequenceAsync);
        DeleteSequenceCommand = new RelayCommand<CraftSequenceSummaryViewModel>(DeleteSequence);
    }

    public ObservableCollection<CraftSequenceSummaryViewModel> Sequences
    {
        get;
    }

    public IAsyncRelayCommand CreateSequenceCommand
    {
        get;
    }

    public IAsyncRelayCommand<CraftSequenceSummaryViewModel> EditSequenceCommand
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
        craftSequenceStore.Delete(sequenceId);
        Refresh();
    }

    private Task CreateSequenceAsync()
    {
        return openEditor(null);
    }

    private Task EditSequenceAsync(CraftSequenceSummaryViewModel? sequence)
    {
        if (sequence is null)
        {
            return Task.CompletedTask;
        }

        return openEditor(craftSequenceStore.Find(sequence.SequenceId));
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
