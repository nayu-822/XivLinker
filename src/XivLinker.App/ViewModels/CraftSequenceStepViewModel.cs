using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.App.ViewModels;

public partial class CraftSequenceStepViewModel : ObservableObject
{
    public CraftSequenceStepViewModel(
        CraftActionDefinition definition,
        ImageSource? iconSource,
        Action<CraftSequenceStepViewModel> remove)
    {
        Definition = definition;
        IconSource = iconSource;
        waitMilliseconds = definition.PostActionWaitMilliseconds;
        RemoveCommand = new RelayCommand(() => remove(this));
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

    [ObservableProperty]
    private int waitMilliseconds;

    public IRelayCommand RemoveCommand
    {
        get;
    }

    public static CraftSequenceStepViewModel FromModel(
        CraftSequenceStep step,
        CraftActionDefinition definition,
        ImageSource? iconSource,
        Action<CraftSequenceStepViewModel> remove)
    {
        return new CraftSequenceStepViewModel(definition, iconSource, remove)
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
