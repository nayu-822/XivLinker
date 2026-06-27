using XivLinker.App.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Services;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class AutoCraftSequenceListViewModelTests
{
    [Fact]
    public void DeleteSequenceCommand_RemovesSequenceFromStore()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "XivLinkerTests", Guid.NewGuid().ToString("N"));

        try
        {
            ICraftSequenceStore store = new CraftSequenceStore(
                new AppDataPathService(rootPath),
                NullLogger<CraftSequenceStore>.Instance);

            var sequence = new CraftSequence
            {
                SequenceId = Guid.NewGuid(),
                Name = "削除テスト",
                Steps =
                [
                    new CraftSequenceStep
                    {
                        ActionId = CraftActionId.BasicSynthesis,
                    },
                ],
            };

            store.Save(sequence);

            var viewModel = new AutoCraftSequenceListViewModel(
                store,
                _ => Task.CompletedTask);

            viewModel.Refresh();
            CraftSequenceSummaryViewModel summary = Assert.Single(viewModel.Sequences);

            viewModel.DeleteSequenceCommand.Execute(summary);

            Assert.Empty(viewModel.Sequences);
            Assert.Null(store.Find(sequence.SequenceId));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
