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

        CraftSequenceValidationResult result =
            await validator.ValidateAsync(sequence, CrafterJobs.Carpenter);

        Assert.False(result.CanRun);
        Assert.Null(result.ErrorMessage);
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

        CraftSequenceValidationResult result =
            await validator.ValidateAsync(sequence, CrafterJobs.Carpenter);

        Assert.True(result.CanRun);
        Assert.Empty(result.MissingActions);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsError_WhenCharacterDataIsMissing()
    {
        var validator = new CraftHotbarRegistrationValidator(new FakeCharacterProfileStore(null));

        CraftSequenceValidationResult result =
            await validator.ValidateAsync(new CraftSequence(), CrafterJobs.Carpenter);

        Assert.False(result.CanRun);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsError_WhenHotbarCannotBeRead()
    {
        CharacterData characterData = CreateCharacterData([], exists: false);
        var validator = new CraftHotbarRegistrationValidator(new FakeCharacterProfileStore(characterData));

        CraftSequenceValidationResult result =
            await validator.ValidateAsync(CreateSequenceWith(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

        Assert.False(result.CanRun);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsError_WhenRegisteredActionsCannotBeResolved()
    {
        CharacterData characterData = CreateCharacterData([0x00, 0x00, 0x00, 0x00], exists: true);
        var validator = new CraftHotbarRegistrationValidator(new FakeCharacterProfileStore(characterData));

        CraftSequenceValidationResult result =
            await validator.ValidateAsync(CreateSequenceWith(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

        Assert.False(result.CanRun);
        Assert.NotNull(result.ErrorMessage);
    }

    private static CraftSequence CreateSequenceWith(CraftActionId actionId)
    {
        return new CraftSequence
        {
            Steps =
            [
                new CraftSequenceStep { ActionId = actionId },
            ],
        };
    }

    private static CharacterData CreateCharacterData(byte[] rawBytes, bool exists = true)
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
                Exists = exists,
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
