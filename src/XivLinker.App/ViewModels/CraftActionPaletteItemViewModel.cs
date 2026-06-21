using CommunityToolkit.Mvvm.Input;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public sealed class CraftActionPaletteItemViewModel
{
    public CraftActionPaletteItemViewModel(
        CraftActionDefinition definition,
        Action<CraftActionId> addToSequence)
    {
        Definition = definition;
        AddToSequenceCommand = new RelayCommand(() => addToSequence(ActionId));
    }

    public CraftActionDefinition Definition
    {
        get;
    }

    public CraftActionId ActionId => Definition.ActionId;

    public string DisplayName => Definition.DisplayName;

    public int WaitMilliseconds => Definition.PostActionWaitMilliseconds;

    public IRelayCommand AddToSequenceCommand
    {
        get;
    }
}
