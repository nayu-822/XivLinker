using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Application.Services;
using XivLinker.Infrastructure.CharacterConfig.Services;

namespace XivLinker.Tests;

public sealed class CharacterProfileStoreTests
{
    [Fact]
    public async Task AddProfileAsync_PersistsAndRestoresProfile()
    {
        string rootPath = CreateAppDataRoot();
        string characterDirectory = CreateCharacterDirectory();

        try
        {
            var store = CreateStore(rootPath);
            await store.AddProfileAsync(characterDirectory, "メインキャラ");

            var reloadedStore = CreateStore(rootPath);
            await reloadedStore.InitializeAsync();

            Assert.Single(reloadedStore.Profiles);
            Assert.Equal("メインキャラ", reloadedStore.Profiles[0].DisplayName);
            Assert.NotNull(reloadedStore.SelectedProfile);
            Assert.Equal(reloadedStore.Profiles[0].Id, reloadedStore.SelectedProfile!.Id);
        }
        finally
        {
            Directory.Delete(rootPath, true);
            Directory.Delete(characterDirectory, true);
        }
    }

    [Fact]
    public async Task UpdateDisplayNameAsync_PersistsAndRestoresName()
    {
        string rootPath = CreateAppDataRoot();
        string characterDirectory = CreateCharacterDirectory();

        try
        {
            var store = CreateStore(rootPath);
            await store.AddProfileAsync(characterDirectory, "初期名");
            string profileId = store.Profiles[0].Id;

            await store.UpdateDisplayNameAsync(profileId, "サブキャラ");

            var reloadedStore = CreateStore(rootPath);
            Assert.Equal("サブキャラ", reloadedStore.Profiles[0].DisplayName);
        }
        finally
        {
            Directory.Delete(rootPath, true);
            Directory.Delete(characterDirectory, true);
        }
    }

    [Fact]
    public async Task RemoveProfileAsync_PersistsRemoval()
    {
        string rootPath = CreateAppDataRoot();
        string characterDirectory = CreateCharacterDirectory();

        try
        {
            var store = CreateStore(rootPath);
            await store.AddProfileAsync(characterDirectory, "削除対象");
            string profileId = store.Profiles[0].Id;

            await store.RemoveProfileAsync(profileId);

            var reloadedStore = CreateStore(rootPath);
            Assert.Empty(reloadedStore.Profiles);
        }
        finally
        {
            Directory.Delete(rootPath, true);
            Directory.Delete(characterDirectory, true);
        }
    }

    [Fact]
    public async Task InitializeAsync_LoadsSelectedProfileData()
    {
        string rootPath = CreateAppDataRoot();
        string characterDirectory = CreateCharacterDirectory();

        try
        {
            var store = CreateStore(rootPath);
            await store.AddProfileAsync(characterDirectory, "読み込み対象");

            var reloadedStore = CreateStore(rootPath);
            await reloadedStore.InitializeAsync();

            Assert.NotNull(reloadedStore.SelectedCharacterData);
            Assert.Empty(reloadedStore.SelectedCharacterData!.Errors);
            Assert.True(reloadedStore.SelectedCharacterData.HotbarAnalysisResult.Exists);
            Assert.True(reloadedStore.SelectedCharacterData.KeybindAnalysisResult.Exists);
        }
        finally
        {
            Directory.Delete(rootPath, true);
            Directory.Delete(characterDirectory, true);
        }
    }

    private static CharacterProfileStore CreateStore(string rootPath)
    {
        return new CharacterProfileStore(
            new CharacterConfigDataService(NullLogger<CharacterConfigDataService>.Instance),
            new AppDataPathService(rootPath),
            NullLogger<CharacterProfileStore>.Instance);
    }

    private static string CreateAppDataRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"xivlinker-character-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private static string CreateCharacterDirectory(bool includeKeybind = true)
    {
        string characterDirectory = Path.Combine(Path.GetTempPath(), $"xivlinker-character-{Guid.NewGuid():N}");
        Directory.CreateDirectory(characterDirectory);
        File.WriteAllBytes(Path.Combine(characterDirectory, "HOTBAR.DAT"), [0x01, 0x02, 0x03]);

        if (includeKeybind)
        {
            File.WriteAllBytes(Path.Combine(characterDirectory, "KEYBIND.DAT"), [0x11, 0x12, 0x13]);
        }

        return characterDirectory;
    }
}
