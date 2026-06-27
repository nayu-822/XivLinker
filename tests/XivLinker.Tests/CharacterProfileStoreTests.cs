using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Infrastructure.CharacterConfig.Services;

namespace XivLinker.Tests;

public sealed class CharacterProfileStoreTests
{
    [Fact]
    public async Task AddProfileAsync_SelectsProfileAndLoadsCharacterData()
    {
        string characterDirectory = CreateCharacterDirectory();

        try
        {
            var dataService = new CharacterConfigDataService(NullLogger<CharacterConfigDataService>.Instance);
            var store = new CharacterProfileStore(dataService, NullLogger<CharacterProfileStore>.Instance);

            await store.AddProfileAsync(characterDirectory);

            Assert.Single(store.Profiles);
            Assert.NotNull(store.SelectedProfile);
            Assert.True(store.SelectedProfile!.IsSelected);
            Assert.NotNull(store.SelectedCharacterData);
            Assert.Empty(store.SelectedCharacterData!.Errors);
            Assert.True(store.SelectedCharacterData.HotbarAnalysisResult.Exists);
            Assert.True(store.SelectedCharacterData.KeybindAnalysisResult.Exists);
        }
        finally
        {
            Directory.Delete(characterDirectory, true);
        }
    }

    [Fact]
    public async Task ReloadProfileAsync_CapturesMissingFilesAsErrors()
    {
        string characterDirectory = CreateCharacterDirectory(includeKeybind: false);

        try
        {
            var dataService = new CharacterConfigDataService(NullLogger<CharacterConfigDataService>.Instance);
            var store = new CharacterProfileStore(dataService, NullLogger<CharacterProfileStore>.Instance);

            await store.AddProfileAsync(characterDirectory);

            Assert.NotNull(store.SelectedCharacterData);
            Assert.NotEmpty(store.SelectedCharacterData!.Errors);
            Assert.Contains(store.SelectedCharacterData.Errors, error => error.Contains("KEYBIND.DAT", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(characterDirectory, true);
        }
    }

    private static string CreateCharacterDirectory(bool includeKeybind = true)
    {
        string characterDirectory = Path.Combine(Path.GetTempPath(), $"xivlinker-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(characterDirectory);
        File.WriteAllBytes(Path.Combine(characterDirectory, "HOTBAR.DAT"), [0x01, 0x02, 0x03]);

        if (includeKeybind)
        {
            File.WriteAllBytes(Path.Combine(characterDirectory, "KEYBIND.DAT"), [0x11, 0x12, 0x13]);
        }

        return characterDirectory;
    }
}
