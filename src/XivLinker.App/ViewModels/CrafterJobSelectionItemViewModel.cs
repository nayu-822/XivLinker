using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public sealed class CrafterJobSelectionItemViewModel
{
    private CrafterJobSelectionItemViewModel(CrafterJob? job, string displayName)
    {
        Job = job;
        DisplayName = displayName;
    }

    public CrafterJob? Job { get; }

    public string DisplayName { get; }

    public static CrafterJobSelectionItemViewModel FromCrafterJob(CrafterJob job)
    {
        return new CrafterJobSelectionItemViewModel(job, $"{job.Name} ({job.ShortName})");
    }

    public static CrafterJobSelectionItemViewModel NonCrafter { get; } =
        new(null, "-");

    public override string ToString()
    {
        return DisplayName;
    }
}
