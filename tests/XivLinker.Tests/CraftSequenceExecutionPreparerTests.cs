using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;
using XivLinker.Infrastructure.CharacterConfig.Models;
using XivLinker.Infrastructure.CharacterConfig.Services;

namespace XivLinker.Tests;

public sealed class CraftSequenceExecutionPreparerTests
{
    [Fact]
    public async Task PrepareAsync_ReturnsUnsupportedFormatError_WhenHotbarAndKeybindFilesExist()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            File.WriteAllBytes(Path.Combine(rootPath, "HOTBAR.DAT"), [0x01, 0x02, 0x03, 0x04]);
            File.WriteAllBytes(Path.Combine(rootPath, "KEYBIND.DAT"), [0x11, 0x12, 0x13, 0x14]);

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Equal(
                "HOTBAR.DAT の実ファイル形式にまだ対応できていないため、シーケンスを準備できません。",
                result.ErrorMessage);
            Assert.Empty(result.ActionKeyBindings);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsError_WhenFilesCannotBeRead()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Equal(
                "HOTBAR.DAT または KEYBIND.DAT を読み込めないため、シーケンスを準備できません。",
                result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void HotbarDatReader_ThrowsUnsupportedFormatException_ForAnyCurrentDatPayload()
    {
        var reader = new HotbarDatReader();

        UnsupportedCharacterConfigFormatException exception = Assert.Throws<UnsupportedCharacterConfigFormatException>(
            () => reader.Read([0x10, 0x20, 0x30], CrafterJobs.Carpenter));

        Assert.Equal(
            "HOTBAR.DAT の実ファイル形式にまだ対応できていないため、シーケンスを準備できません。",
            exception.Message);
    }

    [Fact]
    public void KeybindDatReader_ThrowsUnsupportedFormatException_ForAnyCurrentDatPayload()
    {
        var reader = new KeybindDatReader();

        UnsupportedCharacterConfigFormatException exception = Assert.Throws<UnsupportedCharacterConfigFormatException>(
            () => reader.Read([0x10, 0x20, 0x30]));

        Assert.Equal(
            "KEYBIND.DAT の実ファイル形式にまだ対応できていないため、シーケンスを準備できません。",
            exception.Message);
    }

    private static CraftSequence CreateSequence(CraftActionId actionId)
    {
        return new CraftSequence
        {
            Name = "テスト",
            Steps =
            [
                new CraftSequenceStep { ActionId = actionId },
            ],
        };
    }

    private static CraftSequenceExecutionPreparer CreatePreparer(string characterDirectoryPath)
    {
        var profile = new CharacterProfile
        {
            DisplayName = "Test Character",
            CharacterSettingsDirectory = characterDirectoryPath,
        };

        return new CraftSequenceExecutionPreparer(
            new CharacterConfigFileLoader(new FakeCharacterProfileStore(profile)),
            new HotbarDatReader(),
            new KeybindDatReader(),
            NullLogger<CraftSequenceExecutionPreparer>.Instance);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "XivLinkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeCharacterProfileStore : ICharacterProfileStore
    {
        public FakeCharacterProfileStore(CharacterProfile profile)
        {
            SelectedProfile = profile;
            SelectedCharacterDirectoryPath = profile.CharacterSettingsDirectory;
        }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<CharacterProfile> Profiles => SelectedProfile is null ? [] : [SelectedProfile];

        public CharacterProfile? SelectedProfile { get; }

        public CharacterData? SelectedCharacterData => null;

        public string? SelectedCharacterDirectoryPath { get; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddProfileAsync(string path, string? displayName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SelectProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReloadProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateDisplayNameAsync(string profileId, string? displayName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
