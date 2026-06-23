using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public sealed class CraftActionPaletteItemViewModel
{
    public CraftActionPaletteItemViewModel(
        CraftActionDefinition definition,
        ImageSource? iconSource,
        Action<CraftActionId> addToSequence)
    {
        Definition = definition;
        IconSource = iconSource;
        AddToSequenceCommand = new RelayCommand(() => addToSequence(ActionId));
    }

    public CraftActionDefinition Definition
    {
        get;
    }

    public CraftActionId ActionId => Definition.ActionId;

    public string DisplayName => Definition.DisplayName;

    public ImageSource? IconSource
    {
        get;
    }

    public IRelayCommand AddToSequenceCommand
    {
        get;
    }
}
