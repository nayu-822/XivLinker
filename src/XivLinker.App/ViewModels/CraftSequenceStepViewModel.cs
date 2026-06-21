using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public partial class CraftSequenceStepViewModel : ObservableObject
{
    public CraftSequenceStepViewModel(
        CraftActionDefinition definition,
        Action<CraftSequenceStepViewModel> remove)
    {
        Definition = definition;
        waitMilliseconds = definition.PostActionWaitMilliseconds;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public CraftActionDefinition Definition
    {
        get;
    }

    public CraftActionId ActionId => Definition.ActionId;

    public string DisplayName => Definition.DisplayName;

    [ObservableProperty]
    private int waitMilliseconds;

    public IRelayCommand RemoveCommand
    {
        get;
    }

    public static CraftSequenceStepViewModel FromModel(
        CraftSequenceStep step,
        Action<CraftSequenceStepViewModel> remove)
    {
        CraftActionDefinition definition = CraftActionCatalog.Get(step.ActionId);
        return new CraftSequenceStepViewModel(definition, remove)
        {
            WaitMilliseconds = step.WaitMilliseconds,
        };
    }

    public CraftSequenceStep ToModel()
    {
        return new CraftSequenceStep
        {
            ActionId = ActionId,
            WaitMilliseconds = WaitMilliseconds,
        };
    }
}
