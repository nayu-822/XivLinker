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
            Domain.Models.CrafterJobs.All.Select(CrafterJobSelectionItemViewModel.FromCrafterJob));
        DisplayedCrafterJobs = new ObservableCollection<CrafterJobSelectionItemViewModel>();

        SequenceList = new AutoCraftSequenceListViewModel(
            craftSequenceStore,
            sequence => sequenceEditorDialogService.ShowEditorAsync(sequence, SaveSequence),
            () => SelectedCrafterJob?.Job,
            () => IsCurrentJobNonCrafter,
            craftHotbarRegistrationValidator,
            overlayWindowService,
            autoCraftExecutionService);

        this.currentPlayerStateService.StateChanged += OnCurrentPlayerStateChanged;

        UpdateCurrentCrafterJobFromState();
        RefreshDisplayedCrafterJobs();
        SequenceList.Refresh();
    }

    public AutoCraftSequenceListViewModel SequenceList { get; }

    public ObservableCollection<CrafterJobSelectionItemViewModel> CrafterJobs { get; }

    public ObservableCollection<CrafterJobSelectionItemViewModel> DisplayedCrafterJobs { get; }

    [ObservableProperty]
    private CrafterJobSelectionItemViewModel? selectedCrafterJob;

    [ObservableProperty]
    private CrafterJobDetectionState detectionState;

    public bool IsCrafterJobAutoDetected => DetectionState != CrafterJobDetectionState.Unknown;

    public bool IsCurrentJobNonCrafter => DetectionState == CrafterJobDetectionState.NonCrafter;

    public bool CanChangeCrafterJob => DetectionState == CrafterJobDetectionState.Unknown;

    public string CrafterJobStatusText =>
        DetectionState switch
        {
            CrafterJobDetectionState.Crafter => "現在のクラフター職を自動検出しています。",
            CrafterJobDetectionState.NonCrafter => "現在のジョブがクラフター以外のため、自動クラフトは実行できません。",
            _ => "現在のジョブを自動検出できないため、手動で選択してください。",
        };

    public string Title => "自動クラフト";

    public string Description => "登録済みシーケンスの確認、作成、編集、実行をここから行えます。";

    public void Dispose()
    {
        currentPlayerStateService.StateChanged -= OnCurrentPlayerStateChanged;
    }

    partial void OnDetectionStateChanged(CrafterJobDetectionState value)
    {
        OnPropertyChanged(nameof(IsCrafterJobAutoDetected));
        OnPropertyChanged(nameof(IsCurrentJobNonCrafter));
        OnPropertyChanged(nameof(CanChangeCrafterJob));
        OnPropertyChanged(nameof(CrafterJobStatusText));
        RefreshDisplayedCrafterJobs();
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

        if (classJobId is null or 0)
        {
            DetectionState = CrafterJobDetectionState.Unknown;
            SelectedCrafterJob ??= CrafterJobs.FirstOrDefault();
            return;
        }

        CrafterJob? detectedJob = Domain.Models.CrafterJobs.FindByClassJobId(classJobId.Value);
        if (detectedJob is not null)
        {
            DetectionState = CrafterJobDetectionState.Crafter;
            SelectedCrafterJob = CrafterJobs.FirstOrDefault(item => item.Job?.ClassJobId == detectedJob.ClassJobId);
            return;
        }

        DetectionState = CrafterJobDetectionState.NonCrafter;
        SelectedCrafterJob = CrafterJobSelectionItemViewModel.NonCrafter;
    }

    private void RefreshDisplayedCrafterJobs()
    {
        DisplayedCrafterJobs.Clear();

        if (DetectionState == CrafterJobDetectionState.NonCrafter)
        {
            DisplayedCrafterJobs.Add(CrafterJobSelectionItemViewModel.NonCrafter);
            return;
        }

        foreach (CrafterJobSelectionItemViewModel item in CrafterJobs)
        {
            DisplayedCrafterJobs.Add(item);
        }
    }
}
