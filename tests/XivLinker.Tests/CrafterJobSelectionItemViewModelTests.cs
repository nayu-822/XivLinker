using XivLinker.App.ViewModels;
using XivLinker.Domain.Models;

namespace XivLinker.Tests;

public sealed class CrafterJobSelectionItemViewModelTests
{
    [Fact]
    public void CrafterJobSelectionItemViewModel_ToString_ReturnsDisplayName()
    {
        CrafterJob job = CrafterJobs.All.First();

        CrafterJobSelectionItemViewModel item =
            CrafterJobSelectionItemViewModel.FromCrafterJob(job);

        Assert.Equal(item.DisplayName, item.ToString());
    }

    [Fact]
    public void CrafterJobSelectionItemViewModel_NonCrafter_ToString_ReturnsHyphen()
    {
        Assert.Equal("-", CrafterJobSelectionItemViewModel.NonCrafter.ToString());
    }
}
