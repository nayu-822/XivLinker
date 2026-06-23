using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public sealed partial class CraftActionPaletteItemViewModel : ObservableObject
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

    public uint RepresentativeIconId => Definition.RepresentativeIconId;

    [ObservableProperty]
    private ImageSource? iconSource;

    public IRelayCommand AddToSequenceCommand
    {
        get;
    }
}
