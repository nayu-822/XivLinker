using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.Application.Abstractions;
using XivLinker.App.Services;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public partial class AutoCraftSequenceEditorViewModel : ObservableObject
{
    private const string DefaultActionCategory = "クラフターアクション";

    private readonly ICrafterActionCatalogService crafterActionCatalogService;
    private readonly CraftActionIconSourceService craftActionIconSourceService;
    private readonly Action cancel;
    private readonly Action<CraftSequence> save;
    private readonly Dictionary<CraftActionId, CraftActionDefinition> availableActionDefinitions = [];
    private readonly Dictionary<CraftActionId, ImageSource?> availableActionIcons = [];
    private Guid sequenceId;

    [ObservableProperty]
    private string sequenceName = string.Empty;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isLoadingActions;

    [ObservableProperty]
    private string loadErrorMessage = string.Empty;

    public AutoCraftSequenceEditorViewModel(
        ICrafterActionCatalogService crafterActionCatalogService,
        CraftActionIconSourceService craftActionIconSourceService,
        Action cancel,
        Action<CraftSequence> save)
    {
        this.crafterActionCatalogService = crafterActionCatalogService;
        this.craftActionIconSourceService = craftActionIconSourceService;
        this.cancel = cancel;
        this.save = save;

        AvailableActions = new ObservableCollection<CraftActionPaletteCategoryViewModel>();
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

    public async Task LoadAsync(CraftSequence? sequence, CancellationToken cancellationToken = default)
    {
        await LoadAvailableActionsAsync(cancellationToken);

        IsEditing = sequence is not null;
        sequenceId = sequence?.SequenceId ?? Guid.NewGuid();
        SequenceName = sequence?.Name ?? string.Empty;
        StatusMessage = string.Empty;

        CurrentSteps.Clear();

        if (sequence is not null)
        {
            foreach (CraftSequenceStep step in sequence.Steps)
            {
                CraftActionDefinition definition = ResolveDefinition(step.ActionId);
                CurrentSteps.Add(CraftSequenceStepViewModel.FromModel(
                    step,
                    definition,
                    ResolveIcon(step.ActionId),
                    RemoveStep));
            }
        }

        OnPropertyChanged(nameof(Title));
    }

    public void AddAction(CraftActionId actionId)
    {
        CraftActionDefinition actionDefinition = ResolveDefinition(actionId);
        StatusMessage = string.Empty;
        CurrentSteps.Add(new CraftSequenceStepViewModel(actionDefinition, ResolveIcon(actionId), RemoveStep));
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

    private async Task LoadAvailableActionsAsync(CancellationToken cancellationToken)
    {
        IsLoadingActions = true;
        LoadErrorMessage = string.Empty;

        try
        {
            CrafterActionCatalogResult result = await crafterActionCatalogService.GetCrafterActionsAsync(cancellationToken);

            AvailableActions.Clear();
            availableActionDefinitions.Clear();
            availableActionIcons.Clear();

            if (!result.IsSuccess)
            {
                LoadErrorMessage = result.ErrorMessage ?? "クラフターアクション一覧の取得に失敗しました。";
            }

            CatalogEntry[] entries = await Task.WhenAll(result.Actions.Select(async definition => new CatalogEntry(
                definition,
                await craftActionIconSourceService.GetIconSourceAsync(definition.IconId, cancellationToken))));

            foreach (CatalogEntry entry in entries)
            {
                availableActionDefinitions[entry.Definition.ActionId] = entry.Definition;
                availableActionIcons[entry.Definition.ActionId] = entry.IconSource;
            }

            foreach (IGrouping<string, CatalogEntry> group in entries
                .GroupBy(static entry => string.IsNullOrWhiteSpace(entry.Definition.Category) ? DefaultActionCategory : entry.Definition.Category)
                .OrderBy(static group => group.Key, StringComparer.CurrentCulture))
            {
                AvailableActions.Add(new CraftActionPaletteCategoryViewModel(
                    group.Key,
                    group.Select(entry => new CraftActionPaletteItemViewModel(
                        entry.Definition,
                        entry.IconSource,
                        AddAction))));
            }
        }
        finally
        {
            IsLoadingActions = false;
        }
    }

    private CraftActionDefinition ResolveDefinition(CraftActionId actionId)
    {
        if (availableActionDefinitions.TryGetValue(actionId, out CraftActionDefinition? definition)
            && definition is not null)
        {
            return definition;
        }

        return CreateFallbackDefinition(actionId);
    }

    private ImageSource? ResolveIcon(CraftActionId actionId)
    {
        return availableActionIcons.GetValueOrDefault(actionId);
    }

    private static CraftActionDefinition CreateFallbackDefinition(CraftActionId actionId)
    {
        return actionId.Value switch
        {
            "craftaction:basic-synthesis" => new CraftActionDefinition(actionId, "作業", 2500, DefaultActionCategory, 0),
            "craftaction:basic-touch" => new CraftActionDefinition(actionId, "加工", 2500, DefaultActionCategory, 0),
            "craftaction:masters-mend" => new CraftActionDefinition(actionId, "マスターズメンド", 2500, DefaultActionCategory, 0),
            "craftaction:veneration" => new CraftActionDefinition(actionId, "ヴェネレーション", 2500, DefaultActionCategory, 0),
            "craftaction:innovation" => new CraftActionDefinition(actionId, "イノベーション", 2500, DefaultActionCategory, 0),
            "craftaction:great-strides" => new CraftActionDefinition(actionId, "グレートストライド", 2500, DefaultActionCategory, 0),
            "craftaction:byregots-blessing" => new CraftActionDefinition(actionId, "ビエルゴの祝福", 2500, DefaultActionCategory, 0),
            _ => new CraftActionDefinition(actionId, actionId.Value, 2500, DefaultActionCategory, 0),
        };
    }

    private sealed record CatalogEntry(
        CraftActionDefinition Definition,
        ImageSource? IconSource);
}
