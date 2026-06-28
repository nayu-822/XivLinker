using System.Buffers.Binary;
using System.Text;
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
    public void HotbarDatReader_Read_ParsesEightByteRecords()
    {
        uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
        byte[] datBytes = CreateHotbarDatBytes(
            [CreateHotbarRecord(actionRowId, (byte)CrafterJobs.Carpenter.ClassJobId, 1, 1, (byte)HotbarSlotKind.Action)]);

        var reader = new HotbarDatReader();

        HotbarSlotEntry entry = Assert.Single(reader.Read(datBytes));

        Assert.Equal(actionRowId, entry.CommandId);
        Assert.Equal((byte)CrafterJobs.Carpenter.ClassJobId, entry.GroupId);
        Assert.Equal(1, entry.HotbarId);
        Assert.Equal(1, entry.SlotId);
        Assert.Equal((byte)HotbarSlotKind.Action, entry.SlotTypeId);
    }

    [Fact]
    public void KeybindDatReader_Read_AllowsNonTcSectionTags()
    {
        byte[] datBytes = CreateKeybindDatBytes([('A', "HOTBAR_1_1", 'B', "31.0,")]);

        var reader = new KeybindDatReader();

        KeybindEntry entry = Assert.Single(reader.Read(datBytes));

        Assert.Equal("HOTBAR_1_1", entry.Command);
        Assert.Equal('A', entry.CommandSectionType);
        Assert.Equal('B', entry.BindingSectionType);
        Assert.Equal("31.0,", entry.RawBindingText);
        Assert.Equal("31", entry.Primary?.KeyCode);
        Assert.Equal("0", entry.Primary?.ModifierCode);
    }

    [Fact]
    public void KeybindDatReader_Read_AllowsTrailingNullByteInDecodedBody()
    {
        byte[] datBytes = CreateKeybindDatBytes([('T', "HOTBAR_1_1", 'C', "31.0,")]);

        var reader = new KeybindDatReader();

        KeybindEntry entry = Assert.Single(reader.Read(datBytes));

        Assert.Equal("HOTBAR_1_1", entry.Command);
        Assert.Equal("31.0,", entry.RawBindingText);
    }

    [Fact]
    public void KeybindDatReader_Read_ThrowsWhenSectionNullTerminatorIsMissing()
    {
        byte[] datBytes = CreateKeybindDatBytesWithoutSectionNullTerminator([('T', "HOTBAR_1_1", 'C', "31.0,")]);

        var reader = new KeybindDatReader();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => reader.Read(datBytes));

        Assert.Contains("null terminator", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("HOTBAR_1_1", 0, 0)]
    [InlineData("HOTBAR_1_12", 0, 11)]
    [InlineData("HOTBAR_2_1", 1, 0)]
    [InlineData("HOTBAR_0_0", 0, 0)]
    public void TryResolveHotbarCommand_NormalizesToRawCoordinates(string command, byte expectedHotbarId, byte expectedSlotId)
    {
        bool resolved = KeybindDatReader.TryResolveHotbarCommand(command, out byte hotbarId, out byte slotId);

        Assert.True(resolved);
        Assert.Equal(expectedHotbarId, hotbarId);
        Assert.Equal(expectedSlotId, slotId);
    }

    [Fact]
    public void KeybindDisplayFormatter_FormatsHexKeyAndModifier()
    {
        var gesture = new KeybindGesture("31", "04");

        Assert.Equal("Shift+1", KeybindDisplayFormatter.Format(gesture));
        Assert.Equal(["Shift", "1"], KeybindDisplayFormatter.ToKeys(gesture));
    }

    [Fact]
    public async Task PrepareAsync_ReturnsBindings_WhenHotbarAndKeybindMatch()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes(
                    [CreateHotbarRecord(actionRowId, (byte)CrafterJobs.Carpenter.ClassJobId, 1, 1, (byte)HotbarSlotKind.Action)]),
                CreateKeybindDatBytes([('T', "HOTBAR_1_1", 'C', "31.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.True(result.CanRun);
            CraftActionKeyBinding binding = Assert.Single(result.ActionKeyBindings);
            Assert.Equal("1", binding.KeyGestureText);
            Assert.Empty(result.MissingActions);
            Assert.Empty(result.UnboundActions);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsBindings_WhenHotbarUsesRawZeroBasedCoordinates()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes(
                    [CreateHotbarRecord(actionRowId, (byte)CrafterJobs.Carpenter.ClassJobId, 0, 0, (byte)HotbarSlotKind.Action)]),
                CreateKeybindDatBytes([('T', "HOTBAR_1_1", 'C', "31.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.True(result.CanRun);
            Assert.Single(result.ActionKeyBindings);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsBindings_WhenHotbarUsesOneBasedCoordinates()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes(
                    [CreateHotbarRecord(actionRowId, (byte)CrafterJobs.Carpenter.ClassJobId, 1, 1, (byte)HotbarSlotKind.Action)]),
                CreateKeybindDatBytes([('T', "HOTBAR_1_1", 'C', "31.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.True(result.CanRun);
            Assert.Single(result.ActionKeyBindings);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsMissingActions_WhenHotbarDoesNotContainRequiredAction()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint otherActionRowId = GetLuminaActionId(CraftActionId.BasicTouch, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes(
                    [CreateHotbarRecord(otherActionRowId, (byte)CrafterJobs.Carpenter.ClassJobId, 1, 1, (byte)HotbarSlotKind.Action)]),
                CreateKeybindDatBytes([('T', "HOTBAR_1_1", 'C', "31.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Null(result.ErrorMessage);
            CraftActionRequirement missingAction = Assert.Single(result.MissingActions);
            Assert.Equal(CraftActionId.BasicSynthesis, missingAction.ActionId);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsUnboundActions_WhenHotbarCommandIsNotMappedInKeybind()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes(
                    [CreateHotbarRecord(actionRowId, (byte)CrafterJobs.Carpenter.ClassJobId, 1, 1, (byte)HotbarSlotKind.Action)]),
                CreateKeybindDatBytes([('T', "SOME_OTHER_COMMAND", 'C', "31.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Null(result.ErrorMessage);
            CraftActionRequirement unboundAction = Assert.Single(result.UnboundActions);
            Assert.Equal(CraftActionId.BasicSynthesis, unboundAction.ActionId);
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
    public async Task PrepareAsync_ReturnsError_WhenHotbarFormatIsUnsupported()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            WriteDatFiles(
                rootPath,
                [0xAA, 0xBB, 0xCC],
                CreateKeybindDatBytes([('T', "HOTBAR_1_1", 'C', "31.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Equal("HOTBAR.DAT を読み込めませんでした。", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveRequiredActionsAsync_ReturnsJobSpecificLuminaActionId()
    {
        CraftActionIdResolver resolver = CreateResolver();

        IReadOnlyList<CraftActionRequirement> carpenterRequirements = await resolver.ResolveRequiredActionsAsync(
            CreateSequence(CraftActionId.BasicSynthesis),
            CrafterJobs.Carpenter);
        IReadOnlyList<CraftActionRequirement> blacksmithRequirements = await resolver.ResolveRequiredActionsAsync(
            CreateSequence(CraftActionId.BasicSynthesis),
            CrafterJobs.Blacksmith);

        CraftActionRequirement carpenterRequirement = Assert.Single(carpenterRequirements);
        CraftActionRequirement blacksmithRequirement = Assert.Single(blacksmithRequirements);

        Assert.NotEqual(carpenterRequirement.LuminaActionId, blacksmithRequirement.LuminaActionId);
        Assert.Equal(GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter), carpenterRequirement.LuminaActionId);
        Assert.Equal(GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Blacksmith), blacksmithRequirement.LuminaActionId);
    }

    [Fact]
    public async Task ResolveRequiredActionsAsync_ReturnsZeroLuminaActionId_WhenActionIsUnknown()
    {
        CraftActionIdResolver resolver = CreateResolver();

        IReadOnlyList<CraftActionRequirement> requirements = await resolver.ResolveRequiredActionsAsync(
            CreateSequence(new CraftActionId("craftaction:test-unknown")),
            CrafterJobs.Carpenter);

        CraftActionRequirement requirement = Assert.Single(requirements);

        Assert.Equal(0U, requirement.LuminaActionId);
        Assert.Equal("craftaction:test-unknown", requirement.ActionName);
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
            CreateResolver(),
            new CharacterConfigFileLoader(new FakeCharacterProfileStore(profile)),
            new HotbarDatReader(),
            new KeybindDatReader(),
            NullLogger<CraftSequenceExecutionPreparer>.Instance);
    }

    private static CraftActionIdResolver CreateResolver()
    {
        return new CraftActionIdResolver(NullLogger<CraftActionIdResolver>.Instance);
    }

    private static uint GetLuminaActionId(CraftActionId actionId, CrafterJob crafterJob)
    {
        CraftActionDefinition definition = CraftActionCatalog.Get(actionId);
        CrafterActionVariant variant = definition.Variants.First(item => item.ClassJobRowId == crafterJob.ClassJobId);
        return variant.LuminaRowId;
    }

    private static byte[] CreateHotbarDatBytes(IReadOnlyList<byte[]> records)
    {
        byte[] payload = records.SelectMany(static record => record).ToArray();
        byte[] fileBytes = new byte[16 + payload.Length];

        for (int index = 0; index < payload.Length; index++)
        {
            fileBytes[16 + index] = (byte)(payload[index] ^ 0x31);
        }

        return fileBytes;
    }

    private static byte[] CreateHotbarRecord(
        uint commandId,
        byte groupId,
        byte hotbarId,
        byte slotId,
        byte slotTypeId)
    {
        byte[] record = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0, 4), commandId);
        record[4] = groupId;
        record[5] = hotbarId;
        record[6] = slotId;
        record[7] = slotTypeId;
        return record;
    }

    private static byte[] CreateKeybindDatBytes(IReadOnlyList<(char CommandTag, string Command, char BindingTag, string Binding)> entries)
    {
        using var stream = new MemoryStream();

        foreach ((char commandTag, string command, char bindingTag, string binding) in entries)
        {
            WriteSection(stream, commandTag, command);
            WriteSection(stream, bindingTag, binding);
        }

        return CreateKeybindDatFileBytes(stream.ToArray());
    }

    private static byte[] CreateKeybindDatBytesWithoutSectionNullTerminator(IReadOnlyList<(char CommandTag, string Command, char BindingTag, string Binding)> entries)
    {
        using var stream = new MemoryStream();

        foreach ((char commandTag, string command, char bindingTag, string binding) in entries)
        {
            WriteSection(stream, commandTag, command);
            WriteSectionWithoutNullTerminator(stream, bindingTag, binding);
        }

        return CreateKeybindDatFileBytes(stream.ToArray());
    }

    private static void WriteSection(Stream stream, char type, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text + "\0");
        stream.WriteByte((byte)type);

        Span<byte> sizeBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(sizeBytes, checked((ushort)data.Length));
        stream.Write(sizeBytes);
        stream.Write(data);
    }

    private static void WriteSectionWithoutNullTerminator(Stream stream, char type, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);
        stream.WriteByte((byte)type);

        Span<byte> sizeBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(sizeBytes, checked((ushort)data.Length));
        stream.Write(sizeBytes);
        stream.Write(data);
    }

    private static byte[] CreateKeybindDatFileBytes(byte[] payload)
    {
        byte[] fileBytes = new byte[0x11 + payload.Length + 1];

        uint headerMaxSize = (uint)(fileBytes.Length - 32);
        uint headerValidDataSize = (uint)((0x11 + payload.Length + 1) - 16);

        BinaryPrimitives.WriteUInt32LittleEndian(fileBytes.AsSpan(0x04, 4), headerMaxSize);
        BinaryPrimitives.WriteUInt32LittleEndian(fileBytes.AsSpan(0x08, 4), headerValidDataSize);

        for (int index = 0; index < payload.Length; index++)
        {
            fileBytes[0x11 + index] = (byte)(payload[index] ^ 0x73);
        }

        fileBytes[0x11 + payload.Length] = 0x73;
        return fileBytes;
    }

    private static void WriteDatFiles(string rootPath, byte[] hotbarBytes, byte[] keybindBytes)
    {
        File.WriteAllBytes(Path.Combine(rootPath, "HOTBAR.DAT"), hotbarBytes);
        File.WriteAllBytes(Path.Combine(rootPath, "KEYBIND.DAT"), keybindBytes);
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
