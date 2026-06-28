using XivLinker.Domain.Models;

namespace XivLinker.App.ViewModels;

public sealed class CrafterJobSelectionItemViewModel
{
    public CrafterJobSelectionItemViewModel(CrafterJob job)
    {
        Job = job;
    }

    public CrafterJob Job { get; }

    public string DisplayName => $"{Job.Name} ({Job.ShortName})";
}
