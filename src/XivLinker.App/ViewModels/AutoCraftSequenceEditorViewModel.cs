using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.Domain.Models;

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

    public AutoCraftSequenceEditorViewModel(
        Action cancel,
        Action<CraftSequence> save)
    {
        this.cancel = cancel;
        this.save = save;

        Steps = new ObservableCollection<CraftSequenceStepViewModel>();
        SaveCommand = new RelayCommand(SaveSequence);
        CancelCommand = new RelayCommand(cancel);
        AddStepCommand = new RelayCommand(AddStep);
        DeleteStepCommand = new RelayCommand<CraftSequenceStepViewModel>(DeleteStep);
    }

    public ObservableCollection<CraftSequenceStepViewModel> Steps
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

    public IRelayCommand AddStepCommand
    {
        get;
    }

    public IRelayCommand<CraftSequenceStepViewModel> DeleteStepCommand
    {
        get;
    }

    public string Title => IsEditing ? "シーケンス編集" : "シーケンス新規登録";

    public void Load(CraftSequence? sequence)
    {
        IsEditing = sequence is not null;
        sequenceId = sequence?.SequenceId ?? Guid.NewGuid();
        SequenceName = sequence?.Name ?? string.Empty;

        Steps.Clear();

        if (sequence is null)
        {
            Steps.Add(new CraftSequenceStepViewModel
            {
                ActionName = "作業",
                WaitMilliseconds = 2500,
            });
        }
        else
        {
            foreach (CraftSequenceStep step in sequence.Steps)
            {
                Steps.Add(CraftSequenceStepViewModel.FromModel(step));
            }
        }

        OnPropertyChanged(nameof(Title));
    }

    private void AddStep()
    {
        Steps.Add(new CraftSequenceStepViewModel
        {
            ActionName = string.Empty,
            WaitMilliseconds = 2500,
        });
    }

    private void DeleteStep(CraftSequenceStepViewModel? step)
    {
        if (step is null)
        {
            return;
        }

        Steps.Remove(step);
    }

    private void SaveSequence()
    {
        var sequence = new CraftSequence
        {
            SequenceId = sequenceId,
            Name = string.IsNullOrWhiteSpace(SequenceName) ? "新規シーケンス" : SequenceName.Trim(),
            Steps = Steps
                .Select(static step => step.ToModel())
                .ToArray(),
        };

        save(sequence);
    }
}
