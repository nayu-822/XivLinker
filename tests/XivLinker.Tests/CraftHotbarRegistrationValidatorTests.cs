using XivLinker.Application.Models;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;
using XivLinker.Infrastructure.CharacterConfig.Models;
using XivLinker.Infrastructure.CharacterConfig.Services;

namespace XivLinker.Tests;

public sealed class CraftHotbarRegistrationValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ReturnsMissingActionsOnlyOnce()
    {
        CraftActionDefinition basicSynthesis = CraftActionCatalog.Get(CraftActionId.BasicSynthesis);
        byte[] rawBytes = CreateHotbarBytes([basicSynthesis.Variants[0].LuminaRowId]);
        var validator = new CraftHotbarRegistrationValidator(new FakeCharacterProfileStore(CreateCharacterData(rawBytes)));

        var sequence = new CraftSequence
        {
            Steps =
            [
                new CraftSequenceStep { ActionId = CraftActionId.BasicSynthesis },
                new CraftSequenceStep { ActionId = CraftActionId.BasicTouch },
                new CraftSequenceStep { ActionId = CraftActionId.BasicTouch },
            ],
        };

        Application.Models.CraftSequenceValidationResult result =
            await validator.ValidateAsync(sequence, CrafterJobs.Carpenter);

        Assert.False(result.CanRun);
        CraftActionRequirement missing = Assert.Single(result.MissingActions);
        Assert.Equal(CraftActionId.BasicTouch, missing.ActionId);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsCanRun_WhenAllActionsAreRegistered()
    {
        CraftActionDefinition basicSynthesis = CraftActionCatalog.Get(CraftActionId.BasicSynthesis);
        CraftActionDefinition basicTouch = CraftActionCatalog.Get(CraftActionId.BasicTouch);
        byte[] rawBytes = CreateHotbarBytes(
            [
                basicSynthesis.Variants[0].LuminaRowId,
                basicTouch.Variants[0].LuminaRowId,
            ]);
        var validator = new CraftHotbarRegistrationValidator(new FakeCharacterProfileStore(CreateCharacterData(rawBytes)));

        var sequence = new CraftSequence
        {
            Steps =
            [
                new CraftSequenceStep { ActionId = CraftActionId.BasicSynthesis },
                new CraftSequenceStep { ActionId = CraftActionId.BasicTouch },
            ],
        };

        Application.Models.CraftSequenceValidationResult result =
            await validator.ValidateAsync(sequence, CrafterJobs.Carpenter);

        Assert.True(result.CanRun);
        Assert.Empty(result.MissingActions);
    }

    private static CharacterData CreateCharacterData(byte[] rawBytes)
    {
        return new CharacterData
        {
            Profile = new CharacterProfile
            {
                DisplayName = "Test Character",
                CharacterSettingsDirectory = "C:\\TestCharacter",
            },
            HotbarAnalysisResult = new HotbarAnalysisResult
            {
                FilePath = "C:\\TestCharacter\\HOTBAR.DAT",
                Exists = true,
                RawBytes = rawBytes,
            },
            KeybindAnalysisResult = new KeybindAnalysisResult
            {
                FilePath = "C:\\TestCharacter\\KEYBIND.DAT",
                Exists = false,
            },
            LoadedAt = DateTimeOffset.Now,
            Errors = [],
        };
    }

    private static byte[] CreateHotbarBytes(uint[] luminaRowIds)
    {
        List<byte> bytes = [];

        foreach (uint rowId in luminaRowIds)
        {
            bytes.AddRange(BitConverter.GetBytes(rowId));
            bytes.AddRange([0x00, 0x00, 0x00, 0x00]);
        }

        return [.. bytes];
    }

    private sealed class FakeCharacterProfileStore : ICharacterProfileStore
    {
        public FakeCharacterProfileStore(CharacterData? selectedCharacterData)
        {
            SelectedCharacterData = selectedCharacterData;
        }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<CharacterProfile> Profiles => [];

        public CharacterProfile? SelectedProfile => SelectedCharacterData?.Profile;

        public CharacterData? SelectedCharacterData { get; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddProfileAsync(string path, string? displayName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SelectProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReloadProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateDisplayNameAsync(string profileId, string? displayName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
