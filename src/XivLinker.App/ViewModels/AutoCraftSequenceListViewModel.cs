using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.App.Services;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public sealed class AutoCraftSequenceListViewModel
{
    private const string AutoCraftTitle = "自動クラフト";

    private readonly ICraftSequenceStore craftSequenceStore;
    private readonly Func<CraftSequence?, Task> openEditor;
    private readonly Func<CrafterJob?> getSelectedCrafterJob;
    private readonly Func<bool> isCurrentJobNonCrafter;
    private readonly ICraftSequenceExecutionPreparer craftSequenceExecutionPreparer;
    private readonly OverlayWindowService overlayWindowService;
    private readonly AutoCraftExecutionService autoCraftExecutionService;

    public AutoCraftSequenceListViewModel(
        ICraftSequenceStore craftSequenceStore,
        Func<CraftSequence?, Task> openEditor,
        Func<CrafterJob?> getSelectedCrafterJob,
        Func<bool> isCurrentJobNonCrafter,
        ICraftSequenceExecutionPreparer craftSequenceExecutionPreparer,
        OverlayWindowService overlayWindowService,
        AutoCraftExecutionService autoCraftExecutionService)
    {
        this.craftSequenceStore = craftSequenceStore;
        this.openEditor = openEditor;
        this.getSelectedCrafterJob = getSelectedCrafterJob;
        this.isCurrentJobNonCrafter = isCurrentJobNonCrafter;
        this.craftSequenceExecutionPreparer = craftSequenceExecutionPreparer;
        this.overlayWindowService = overlayWindowService;
        this.autoCraftExecutionService = autoCraftExecutionService;

        Sequences = new ObservableCollection<CraftSequenceSummaryViewModel>();
        CreateSequenceCommand = new AsyncRelayCommand(CreateSequenceAsync);
        EditSequenceCommand = new AsyncRelayCommand<CraftSequenceSummaryViewModel>(EditSequenceAsync);
        DeleteSequenceCommand = new RelayCommand<CraftSequenceSummaryViewModel>(DeleteSequence);
        RunSequenceCommand = new AsyncRelayCommand<CraftSequenceSummaryViewModel>(RunSequenceAsync, CanRunSequence);

        autoCraftExecutionService.StateChanged += (_, _) => RunSequenceCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<CraftSequenceSummaryViewModel> Sequences { get; }

    public IAsyncRelayCommand CreateSequenceCommand { get; }

    public IAsyncRelayCommand<CraftSequenceSummaryViewModel> EditSequenceCommand { get; }

    public IRelayCommand<CraftSequenceSummaryViewModel> DeleteSequenceCommand { get; }

    public IAsyncRelayCommand<CraftSequenceSummaryViewModel> RunSequenceCommand { get; }

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

    private bool CanRunSequence(CraftSequenceSummaryViewModel? sequence)
    {
        return sequence is not null && !autoCraftExecutionService.IsRunning;
    }

    private async Task RunSequenceAsync(CraftSequenceSummaryViewModel? sequence)
    {
        if (sequence is null || autoCraftExecutionService.IsRunning)
        {
            return;
        }

        CraftSequence? fullSequence = craftSequenceStore.Find(sequence.SequenceId);
        if (fullSequence is null)
        {
            return;
        }

        if (isCurrentJobNonCrafter())
        {
            await overlayWindowService.ShowMessageAsync(
                AutoCraftTitle,
                "現在のジョブがクラフターではないため、このシーケンスは実行できません。");
            return;
        }

        CrafterJob? crafterJob = getSelectedCrafterJob();
        if (crafterJob is null)
        {
            await overlayWindowService.ShowMessageAsync(
                AutoCraftTitle,
                "現在のジョブを自動判定できないため、クラフター職を選択してから実行してください。");
            return;
        }

        CraftSequenceExecutionPreparationResult preparationResult =
            await craftSequenceExecutionPreparer.PrepareAsync(fullSequence, crafterJob);

        if (!preparationResult.CanRun)
        {
            if (!string.IsNullOrWhiteSpace(preparationResult.ErrorMessage))
            {
                await overlayWindowService.ShowMessageAsync(
                    AutoCraftTitle,
                    preparationResult.ErrorMessage);
                return;
            }

            await overlayWindowService.ShowCraftExecutionPreparationFailedAsync(
                fullSequence.Name,
                crafterJob.Name,
                preparationResult.MissingActions,
                preparationResult.UnboundActions);
            return;
        }

        int? runCount = await overlayWindowService.ShowRunOptionsAsync(fullSequence.Name);
        if (runCount is null)
        {
            return;
        }

        await autoCraftExecutionService.StartAsync(new AutoCraftExecutionContext
        {
            Sequence = fullSequence,
            RunCount = runCount.Value,
            CrafterJob = crafterJob,
            ActionKeyBindings = preparationResult.ActionKeyBindings,
        });
    }
}
