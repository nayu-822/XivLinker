using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.App.Services;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public sealed class AutoCraftSequenceListViewModel
{
    private readonly ICraftSequenceStore craftSequenceStore;
    private readonly Func<CraftSequence?, Task> openEditor;
    private readonly Func<CrafterJob?>? getSelectedCrafterJob;
    private readonly Func<bool>? isCurrentJobNonCrafter;
    private readonly ICraftHotbarRegistrationValidator? hotbarRegistrationValidator;
    private readonly OverlayWindowService? overlayWindowService;
    private readonly AutoCraftExecutionService? autoCraftExecutionService;

    public AutoCraftSequenceListViewModel(
        ICraftSequenceStore craftSequenceStore,
        Func<CraftSequence?, Task> openEditor,
        Func<CrafterJob?>? getSelectedCrafterJob = null,
        Func<bool>? isCurrentJobNonCrafter = null,
        ICraftHotbarRegistrationValidator? hotbarRegistrationValidator = null,
        OverlayWindowService? overlayWindowService = null,
        AutoCraftExecutionService? autoCraftExecutionService = null)
    {
        this.craftSequenceStore = craftSequenceStore;
        this.openEditor = openEditor;
        this.getSelectedCrafterJob = getSelectedCrafterJob;
        this.isCurrentJobNonCrafter = isCurrentJobNonCrafter;
        this.hotbarRegistrationValidator = hotbarRegistrationValidator;
        this.overlayWindowService = overlayWindowService;
        this.autoCraftExecutionService = autoCraftExecutionService;

        Sequences = new ObservableCollection<CraftSequenceSummaryViewModel>();
        CreateSequenceCommand = new AsyncRelayCommand(CreateSequenceAsync);
        EditSequenceCommand = new AsyncRelayCommand<CraftSequenceSummaryViewModel>(EditSequenceAsync);
        DeleteSequenceCommand = new RelayCommand<CraftSequenceSummaryViewModel>(DeleteSequence);
        RunSequenceCommand = new AsyncRelayCommand<CraftSequenceSummaryViewModel>(RunSequenceAsync, CanRunSequence);

        if (autoCraftExecutionService is not null)
        {
            autoCraftExecutionService.StateChanged += (_, _) => RunSequenceCommand.NotifyCanExecuteChanged();
        }
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
        return sequence is not null && autoCraftExecutionService?.IsRunning != true;
    }

    private async Task RunSequenceAsync(CraftSequenceSummaryViewModel? sequence)
    {
        if (sequence is null || overlayWindowService is null || autoCraftExecutionService is null)
        {
            return;
        }

        if (autoCraftExecutionService.IsRunning)
        {
            return;
        }

        CraftSequence? fullSequence = craftSequenceStore.Find(sequence.SequenceId);
        if (fullSequence is null)
        {
            return;
        }

        if (isCurrentJobNonCrafter?.Invoke() == true)
        {
            await overlayWindowService.ShowMessageAsync(
                "自動クラフト",
                "現在のジョブがクラフター以外のため、このシーケンスは実行できません。");
            return;
        }

        CrafterJob? crafterJob = getSelectedCrafterJob?.Invoke();
        if (crafterJob is null)
        {
            await overlayWindowService.ShowMessageAsync(
                "自動クラフト",
                "現在のジョブを自動検出できないため、クラフター職を選択してから実行してください。");
            return;
        }

        if (hotbarRegistrationValidator is not null)
        {
            Application.Models.CraftSequenceValidationResult validationResult =
                await hotbarRegistrationValidator.ValidateAsync(fullSequence, crafterJob);

            if (!validationResult.CanRun)
            {
                await overlayWindowService.ShowMissingHotbarActionsAsync(
                    fullSequence.Name,
                    crafterJob.Name,
                    validationResult.MissingActions);
                return;
            }
        }

        int? runCount = await overlayWindowService.ShowRunOptionsAsync(fullSequence.Name);
        if (runCount is null)
        {
            return;
        }

        await autoCraftExecutionService.StartAsync(fullSequence, runCount.Value);
    }
}
