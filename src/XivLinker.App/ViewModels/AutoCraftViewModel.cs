using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XivLinker.Application.Abstractions;
using XivLinker.App.Services;
using XivLinker.Domain.Models;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public partial class AutoCraftViewModel : ObservableObject, IDisposable
{
    private readonly IOverlayPluginCurrentPlayerStateService currentPlayerStateService;

    public AutoCraftViewModel(
        ICraftSequenceStore craftSequenceStore,
        IAutoCraftSequenceEditorDialogService sequenceEditorDialogService,
        OverlayWindowService overlayWindowService,
        AutoCraftExecutionService autoCraftExecutionService,
        ICraftHotbarRegistrationValidator craftHotbarRegistrationValidator,
        IOverlayPluginCurrentPlayerStateService currentPlayerStateService)
    {
        this.currentPlayerStateService = currentPlayerStateService;
        CrafterJobs = new ObservableCollection<CrafterJobSelectionItemViewModel>(
            Domain.Models.CrafterJobs.All.Select(static job => new CrafterJobSelectionItemViewModel(job)));

        SequenceList = new AutoCraftSequenceListViewModel(
            craftSequenceStore,
            sequence => sequenceEditorDialogService.ShowEditorAsync(sequence, SaveSequence),
            () => SelectedCrafterJob?.Job,
            craftHotbarRegistrationValidator,
            overlayWindowService,
            autoCraftExecutionService);

        this.currentPlayerStateService.StateChanged += OnCurrentPlayerStateChanged;
        UpdateCurrentCrafterJobFromState();
        SequenceList.Refresh();
    }

    public AutoCraftSequenceListViewModel SequenceList { get; }

    public ObservableCollection<CrafterJobSelectionItemViewModel> CrafterJobs { get; }

    [ObservableProperty]
    private CrafterJobSelectionItemViewModel? selectedCrafterJob;

    [ObservableProperty]
    private bool isCrafterJobAutoDetected;

    public bool CanChangeCrafterJob => !IsCrafterJobAutoDetected;

    public string CrafterJobStatusText =>
        IsCrafterJobAutoDetected
            ? "現在のジョブを自動検出しています。"
            : "現在のジョブを自動検出できないため、手動で選択してください。";

    public string Title => "自動クラフト";

    public string Description => "登録済みシーケンスの確認、作成、編集、実行をここから行えます。";

    public void Dispose()
    {
        currentPlayerStateService.StateChanged -= OnCurrentPlayerStateChanged;
    }

    partial void OnIsCrafterJobAutoDetectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanChangeCrafterJob));
        OnPropertyChanged(nameof(CrafterJobStatusText));
    }

    private void SaveSequence(CraftSequence sequence)
    {
        SequenceList.Save(sequence);
    }

    private void OnCurrentPlayerStateChanged(object? sender, EventArgs e)
    {
        UpdateCurrentCrafterJobFromState();
    }

    private void UpdateCurrentCrafterJobFromState()
    {
        uint? classJobId = currentPlayerStateService.CurrentState.ClassJobId;
        CrafterJob? detectedJob = classJobId is not null
            ? Domain.Models.CrafterJobs.FindByClassJobId(classJobId.Value)
            : null;

        if (detectedJob is null)
        {
            IsCrafterJobAutoDetected = false;
            return;
        }

        SelectedCrafterJob = CrafterJobs.FirstOrDefault(x => x.Job.ClassJobId == detectedJob.ClassJobId);
        IsCrafterJobAutoDetected = true;
    }
}
