using XivLinker.Application.Services;
using XivLinker.Domain.Models;

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
                    ActionName = "加工",
                    WaitMilliseconds = 2500,
                },
            ],
        };

        store.Save(sequence);

        CraftSequence? saved = store.Find(sequence.SequenceId);

        Assert.NotNull(saved);
        Assert.Equal(sequence.Name, saved.Name);
        Assert.Single(saved.Steps);
    }

    [Fact]
    public void Delete_RemovesSequence()
    {
        var store = new CraftSequenceStore();
        var sequence = new CraftSequence
        {
            SequenceId = Guid.NewGuid(),
            Name = "削除対象",
            Steps =
            [
                new CraftSequenceStep
                {
                    ActionName = "加工",
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
