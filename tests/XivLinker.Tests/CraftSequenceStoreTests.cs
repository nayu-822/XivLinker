using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Application.Services;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Tests;

public sealed class CraftSequenceStoreTests
{
    [Fact]
    public void Save_PersistsSequenceAndRestoresIt()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            var store = new CraftSequenceStore(new AppDataPathService(rootPath), NullLogger<CraftSequenceStore>.Instance);
            var sequence = new CraftSequence
            {
                SequenceId = Guid.NewGuid(),
                Name = "テストシーケンス",
                Steps =
                [
                    new CraftSequenceStep
                    {
                        ActionId = CraftActionId.BasicTouch,
                    },
                ],
            };

            store.Save(sequence);

            var reloadedStore = new CraftSequenceStore(new AppDataPathService(rootPath), NullLogger<CraftSequenceStore>.Instance);
            CraftSequence? saved = reloadedStore.Find(sequence.SequenceId);

            Assert.NotNull(saved);
            Assert.Equal(sequence.Name, saved.Name);
            Assert.Single(saved.Steps);
            Assert.Equal(CraftActionId.BasicTouch, saved.Steps[0].ActionId);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public void Save_UpdatesPersistedSequence()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            var store = new CraftSequenceStore(new AppDataPathService(rootPath), NullLogger<CraftSequenceStore>.Instance);
            var sequence = new CraftSequence
            {
                SequenceId = Guid.NewGuid(),
                Name = "更新前",
                Steps =
                [
                    new CraftSequenceStep
                    {
                        ActionId = CraftActionId.BasicSynthesis,
                    },
                ],
            };

            store.Save(sequence);
            sequence.Name = "更新後";
            sequence.Steps =
            [
                new CraftSequenceStep
                {
                    ActionId = CraftActionId.BasicTouch,
                },
            ];
            store.Save(sequence);

            var reloadedStore = new CraftSequenceStore(new AppDataPathService(rootPath), NullLogger<CraftSequenceStore>.Instance);
            CraftSequence? saved = reloadedStore.Find(sequence.SequenceId);

            Assert.NotNull(saved);
            Assert.Equal("更新後", saved.Name);
            Assert.Single(saved.Steps);
            Assert.Equal(CraftActionId.BasicTouch, saved.Steps[0].ActionId);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public void Delete_RemovesPersistedSequence()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            var store = new CraftSequenceStore(new AppDataPathService(rootPath), NullLogger<CraftSequenceStore>.Instance);
            var sequence = new CraftSequence
            {
                SequenceId = Guid.NewGuid(),
                Name = "削除対象",
                Steps =
                [
                    new CraftSequenceStep
                    {
                        ActionId = CraftActionId.BasicSynthesis,
                    },
                ],
            };

            store.Save(sequence);
            store.Delete(sequence.SequenceId);

            var reloadedStore = new CraftSequenceStore(new AppDataPathService(rootPath), NullLogger<CraftSequenceStore>.Instance);
            Assert.Null(reloadedStore.Find(sequence.SequenceId));
            Assert.Empty(reloadedStore.GetAll());
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public void Constructor_WithBrokenJson_DoesNotThrowAndBacksUpFile()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            string filePath = new AppDataPathService(rootPath).CraftSequencesFilePath;
            File.WriteAllText(filePath, "{broken json");

            var store = new CraftSequenceStore(new AppDataPathService(rootPath), NullLogger<CraftSequenceStore>.Instance);

            Assert.Empty(store.GetAll());
            string[] backups = Directory.GetFiles(rootPath, "craft-sequences.json.*.bak");
            Assert.Single(backups);
            Assert.False(File.Exists(filePath));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    private static string CreateAppDataRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"xivlinker-craft-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
}
