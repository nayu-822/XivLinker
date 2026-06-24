using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.Application.Abstractions;
using XivLinker.App.Services;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public partial class AutoCraftSequenceEditorViewModel : ObservableObject
{
    private readonly ICrafterActionCatalogService crafterActionCatalogService;
    private readonly CraftActionIconSourceService craftActionIconSourceService;
    private readonly Action<CraftSequence> save;
    private readonly Dictionary<CraftActionId, CraftActionDefinition> availableActionDefinitions = [];
    private CancellationTokenSource? iconLoadingCancellationTokenSource;
    private Guid sequenceId;
    private bool suppressDirtyTracking;

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

    [ObservableProperty]
    private bool hasUnsavedChanges;

    public AutoCraftSequenceEditorViewModel(
        ICrafterActionCatalogService crafterActionCatalogService,
        CraftActionIconSourceService craftActionIconSourceService,
        Action cancel,
        Action<CraftSequence> save)
    {
        this.crafterActionCatalogService = crafterActionCatalogService;
        this.craftActionIconSourceService = craftActionIconSourceService;
        this.save = save;

        AvailableActions = new ObservableCollection<CraftActionPaletteCategoryViewModel>();
        CurrentSteps = new ObservableCollection<CraftSequenceStepViewModel>();
        CurrentSteps.CollectionChanged += OnCurrentStepsChanged;
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

    public string WindowTitle => IsEditing ? "クラフトシーケンスを編集" : "クラフトシーケンスを新規作成";

    public async Task LoadAsync(CraftSequence? sequence, CancellationToken cancellationToken = default)
    {
        CancelIconLoading();
        iconLoadingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken iconLoadingToken = iconLoadingCancellationTokenSource.Token;
        suppressDirtyTracking = true;

        IsEditing = sequence is not null;
        sequenceId = sequence?.SequenceId ?? Guid.NewGuid();
        SequenceName = sequence?.Name ?? string.Empty;
        StatusMessage = string.Empty;
        CurrentSteps.Clear();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(WindowTitle));

        await LoadAvailableActionsAsync(iconLoadingToken);

        if (sequence is not null)
        {
            foreach (CraftSequenceStep step in sequence.Steps)
            {
                CraftActionDefinition definition = ResolveDefinition(step.ActionId);
                var stepViewModel = CraftSequenceStepViewModel.FromModel(step, definition, RemoveStep);
                CurrentSteps.Add(stepViewModel);
                _ = LoadIconForStepAsync(stepViewModel, iconLoadingToken);
            }
        }

        suppressDirtyTracking = false;
        MarkClean();
    }

    public void AddAction(CraftActionId actionId)
    {
        CraftActionDefinition actionDefinition = ResolveDefinition(actionId);
        StatusMessage = string.Empty;

        var stepViewModel = new CraftSequenceStepViewModel(actionDefinition, RemoveStep);
        CurrentSteps.Add(stepViewModel);

        CancellationToken cancellationToken = iconLoadingCancellationTokenSource?.Token ?? CancellationToken.None;
        _ = LoadIconForStepAsync(stepViewModel, cancellationToken);
    }

    public void MarkClean()
    {
        HasUnsavedChanges = false;
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
            StatusMessage = "シーケンスにアクションを1つ以上追加してください。";
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

            if (!result.IsSuccess)
            {
                LoadErrorMessage = result.ErrorMessage ?? "クラフターアクション一覧の取得に失敗しました。";
            }

            foreach (CraftActionDefinition definition in result.Actions)
            {
                availableActionDefinitions[definition.ActionId] = definition;
            }

            foreach (CrafterActionDefinitions.CrafterActionPaletteCategoryDefinition category in CrafterActionDefinitions.PaletteCategories)
            {
                var itemViewModels = category.ActionIds
                    .Select(actionId => availableActionDefinitions.GetValueOrDefault(actionId))
                    .Where(static definition => definition is not null)
                    .Cast<CraftActionDefinition>()
                    .Select(definition => new CraftActionPaletteItemViewModel(definition, AddAction))
                    .ToArray();

                if (itemViewModels.Length == 0)
                {
                    continue;
                }

                AvailableActions.Add(new CraftActionPaletteCategoryViewModel(category.CategoryName, itemViewModels));

                foreach (CraftActionPaletteItemViewModel itemViewModel in itemViewModels)
                {
                    _ = LoadIconForPaletteItemAsync(itemViewModel, cancellationToken);
                }
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

        if (CraftActionCatalog.TryGet(actionId, out CraftActionDefinition? fallback)
            && fallback is not null)
        {
            return fallback;
        }

        return new CraftActionDefinition(
            actionId,
            actionId.Value,
            CraftActionCatalog.DefaultPostActionWaitMilliseconds,
            "クラフターアクション",
            0,
            []);
    }

    private async Task LoadIconForPaletteItemAsync(
        CraftActionPaletteItemViewModel itemViewModel,
        CancellationToken cancellationToken)
    {
        try
        {
            itemViewModel.IconSource = await craftActionIconSourceService.GetIconSourceAsync(
                itemViewModel.RepresentativeIconId,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            itemViewModel.IconSource = null;
        }
        catch
        {
            itemViewModel.IconSource = null;
        }
    }

    private async Task LoadIconForStepAsync(
        CraftSequenceStepViewModel stepViewModel,
        CancellationToken cancellationToken)
    {
        try
        {
            stepViewModel.IconSource = await craftActionIconSourceService.GetIconSourceAsync(
                stepViewModel.RepresentativeIconId,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            stepViewModel.IconSource = null;
        }
        catch
        {
            stepViewModel.IconSource = null;
        }
    }

    private void CancelIconLoading()
    {
        if (iconLoadingCancellationTokenSource is null)
        {
            return;
        }

        iconLoadingCancellationTokenSource.Cancel();
        iconLoadingCancellationTokenSource.Dispose();
        iconLoadingCancellationTokenSource = null;
    }

    partial void OnSequenceNameChanged(string value)
    {
        if (suppressDirtyTracking)
        {
            return;
        }

        HasUnsavedChanges = true;
    }

    private void OnCurrentStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (suppressDirtyTracking)
        {
            return;
        }

        HasUnsavedChanges = true;
    }
}
