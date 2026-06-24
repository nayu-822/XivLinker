using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public sealed partial class CraftSequenceStepViewModel : ObservableObject
{
    public CraftSequenceStepViewModel(
        CraftActionDefinition definition,
        Action<CraftSequenceStepViewModel> remove)
    {
        Definition = definition;
        RemoveCommand = new RelayCommand(() => remove(this));
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

    public IRelayCommand RemoveCommand
    {
        get;
    }

    public static CraftSequenceStepViewModel FromModel(
        CraftSequenceStep step,
        CraftActionDefinition definition,
        Action<CraftSequenceStepViewModel> remove)
    {
        return new CraftSequenceStepViewModel(definition, remove);
    }

    public CraftSequenceStep ToModel()
    {
        return new CraftSequenceStep
        {
            ActionId = ActionId,
        };
    }
}
