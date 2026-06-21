using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public partial class AutoCraftSequenceEditorViewModel : ObservableObject
{
    private readonly Action cancel;
    private readonly Action<CraftSequence> save;
    private Guid sequenceId;

    [ObservableProperty]
    private string sequenceName = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public AutoCraftSequenceEditorViewModel(
        Action cancel,
        Action<CraftSequence> save)
    {
        this.cancel = cancel;
        this.save = save;

        AvailableActions = new ObservableCollection<CraftActionPaletteCategoryViewModel>(
            CraftActionCatalog.GetAll()
                .GroupBy(static definition => definition.Category)
                .Select(group => new CraftActionPaletteCategoryViewModel(
                    group.Key,
                    group.Select(definition => new CraftActionPaletteItemViewModel(definition, AddAction)))));

        CurrentSteps = new ObservableCollection<CraftSequenceStepViewModel>();
        SaveCommand = new RelayCommand(SaveSequence);
        CancelCommand = new RelayCommand(cancel);
    }

    public ObservableCollection<CraftActionPaletteCategoryViewModel> AvailableActions
    {
        get;
    }

    public ObservableCollection<CraftSequenceStepViewModel> CurrentSteps
    {
        get;
    }

    public IRelayCommand SaveCommand
    {
        get;
    }

    public IRelayCommand CancelCommand
    {
        get;
    }

    public string Title => IsEditing ? "シーケンス編集" : "シーケンス新規作成";

    public void Load(CraftSequence? sequence)
    {
        IsEditing = sequence is not null;
        sequenceId = sequence?.SequenceId ?? Guid.NewGuid();
        SequenceName = sequence?.Name ?? string.Empty;
        StatusMessage = string.Empty;

        CurrentSteps.Clear();

        if (sequence is not null)
        {
            foreach (CraftSequenceStep step in sequence.Steps)
            {
                CurrentSteps.Add(CraftSequenceStepViewModel.FromModel(step, RemoveStep));
            }
        }

        OnPropertyChanged(nameof(Title));
    }

    public void AddAction(CraftActionId actionId)
    {
        CraftActionDefinition actionDefinition = CraftActionCatalog.Get(actionId);
        StatusMessage = string.Empty;
        CurrentSteps.Add(new CraftSequenceStepViewModel(actionDefinition, RemoveStep));
    }

    private void RemoveStep(CraftSequenceStepViewModel? step)
    {
        if (step is null)
        {
            return;
        }

        StatusMessage = string.Empty;
        CurrentSteps.Remove(step);
    }

    private void SaveSequence()
    {
        if (CurrentSteps.Count == 0)
        {
            StatusMessage = "シーケンスにアクションを1件以上追加してください。";
            return;
        }

        if (CurrentSteps.Any(step => step.WaitMilliseconds < 1))
        {
            StatusMessage = "待機時間は1ms以上で入力してください。";
            return;
        }

        var sequence = new CraftSequence
        {
            SequenceId = sequenceId,
            Name = string.IsNullOrWhiteSpace(SequenceName) ? "新規シーケンス" : SequenceName.Trim(),
            Steps = CurrentSteps
                .Select(static step => step.ToModel())
                .ToArray(),
        };

        StatusMessage = string.Empty;
        save(sequence);
    }
}
