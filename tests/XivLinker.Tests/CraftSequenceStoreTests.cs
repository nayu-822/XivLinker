using XivLinker.Application.Services;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class CraftSequenceStoreTests
{
    [Fact]
    public void Save_StoresSequence()
    {
        var store = new CraftSequenceStore();
        var sequence = new CraftSequence
        {
            SequenceId = Guid.NewGuid(),
            Name = "テストシーケンス",
            Steps =
            [
                new CraftSequenceStep
                {
                    ActionId = CraftActionId.BasicTouch,
                    WaitMilliseconds = 2500,
                },
            ],
        };

        store.Save(sequence);

        CraftSequence? saved = store.Find(sequence.SequenceId);

        Assert.NotNull(saved);
        Assert.Equal(sequence.Name, saved.Name);
        Assert.Single(saved.Steps);
        Assert.Equal(CraftActionId.BasicTouch, saved.Steps[0].ActionId);
    }

    [Fact]
    public void Delete_RemovesSequence()
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

        store.Delete(sequence.SequenceId);

        Assert.Null(store.Find(sequence.SequenceId));
        Assert.Empty(store.GetAll());
    }
}
