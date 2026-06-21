using XivLinker.App.ViewModels;
using XivLinker.Application.Services;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class AutoCraftSequenceListViewModelTests
{
    [Fact]
    public void DeleteSequenceCommand_RemovesSequenceFromStore()
    {
        var store = new CraftSequenceStore();
        var sequence = new CraftSequence
        {
            SequenceId = Guid.NewGuid(),
            Name = "削除テスト",
            Steps =
            [
                new CraftSequenceStep
                {
                    ActionId = CraftActionId.BasicSynthesis,
                    WaitMilliseconds = 2500,
                },
            ],
        };

        store.Save(sequence);

        var viewModel = new AutoCraftSequenceListViewModel(
            store,
            _ => { });

        viewModel.Refresh();
        CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

        viewModel.DeleteSequenceCommand.Execute(summary);

        Assert.Empty(viewModel.Sequences);
        Assert.Null(store.Find(sequence.SequenceId));
    }
}
