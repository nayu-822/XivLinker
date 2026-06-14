using CommunityToolkit.Mvvm.ComponentModel;
using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public partial class CraftSequenceStepViewModel : ObservableObject
{
    [ObservableProperty]
    private string actionName = string.Empty;

    [ObservableProperty]
    private int waitMilliseconds = 2500;

    public static CraftSequenceStepViewModel FromModel(CraftSequenceStep step)
    {
        return new CraftSequenceStepViewModel
        {
            ActionName = step.ActionName,
            WaitMilliseconds = step.WaitMilliseconds,
        };
    }

    public CraftSequenceStep ToModel()
    {
        return new CraftSequenceStep
        {
            ActionName = ActionName.Trim(),
            WaitMilliseconds = WaitMilliseconds,
        };
    }
}
