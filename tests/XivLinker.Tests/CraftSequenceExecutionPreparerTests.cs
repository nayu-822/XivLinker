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
    public void KeybindDatReader_Read_ParsesSections()
    {
        byte[] datBytes = CreateDatFileBytes(
            xorKey: 0x73,
            payload: CreateKeybindContent(("HOTBAR_1_1", "1.0,0.0,")));

        var reader = new KeybindDatReader();

        KeybindEntry entry = Assert.Single(reader.Read(datBytes));

        Assert.Equal("HOTBAR_1_1", entry.Command);
        Assert.NotNull(entry.Primary);
        Assert.Equal("1", entry.Primary!.DisplayText);
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
                CreateHotbarDatBytes([CreateHotbarRecord(1, 1, HotbarSlotKind.Action, actionRowId, CrafterJobs.Carpenter.ClassJobId)]),
                CreateKeybindDatBytes([("HOTBAR_1_1", "1.0,0.0,")]));

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
    public async Task PrepareAsync_ReturnsError_WhenHotbarDoesNotContainRequiredAction()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint otherActionRowId = GetLuminaActionId(CraftActionId.BasicTouch, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes([CreateHotbarRecord(1, 1, HotbarSlotKind.Action, otherActionRowId, CrafterJobs.Carpenter.ClassJobId)]),
                CreateKeybindDatBytes([("HOTBAR_1_1", "1.0,0.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Equal("HOTBAR.DAT を解析できないため、シーケンスを準備できません。", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsUnboundActions_WhenKeybindIsMissing()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes([CreateHotbarRecord(1, 1, HotbarSlotKind.Action, actionRowId, CrafterJobs.Carpenter.ClassJobId)]),
                CreateKeybindDatBytes([("OTHER_COMMAND", "1.0,0.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Equal(
                "ホットバーのキーバインドを取得できませんでした。KEYBIND.DAT のcommand解析ログを確認してください。",
                result.ErrorMessage);
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
                CreateDatFileBytes(0x31, [0xAA, 0xBB, 0xCC]),
                CreateKeybindDatBytes([("HOTBAR_1_1", "1.0,0.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Equal("HOTBAR.DAT を解析できないため、シーケンスを準備できません。", result.ErrorMessage);
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
    public async Task PrepareAsync_ReturnsBindings_WhenCurrentJobVariantMatchesHotbarAction()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint blacksmithActionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Blacksmith);
            WriteDatFiles(
                rootPath,
                CreateHotbarDatBytes([CreateHotbarRecord(1, 1, HotbarSlotKind.Action, blacksmithActionRowId, CrafterJobs.Blacksmith.ClassJobId)]),
                CreateKeybindDatBytes([("HOTBAR_1_1", "1.0,0.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Blacksmith);

            Assert.True(result.CanRun);
            CraftActionKeyBinding binding = Assert.Single(result.ActionKeyBindings);
            Assert.Equal(CraftActionId.BasicSynthesis, binding.ActionId);
            Assert.Equal("1", binding.KeyGestureText);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsBindings_WhenHotbarUsesActionIdAnchorLayout()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteDatFiles(
                rootPath,
                CreateDatFileBytes(0x31, CreateAnchoredHotbarPayload(1, 1, HotbarSlotKind.Action, actionRowId, CrafterJobs.Carpenter.ClassJobId)),
                CreateKeybindDatBytes([("HOTBAR_1_1", "1.0,0.0,")]));

            CraftSequenceExecutionPreparer preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.True(result.CanRun);
            CraftActionKeyBinding binding = Assert.Single(result.ActionKeyBindings);
            Assert.Equal(CraftActionId.BasicSynthesis, binding.ActionId);
            Assert.Equal("1", binding.KeyGestureText);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
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
        return CreateDatFileBytes(0x31, payload);
    }

    private static byte[] CreateHotbarRecord(
        byte hotbarNumber,
        byte slotNumber,
        HotbarSlotKind kind,
        uint actionId,
        uint classJobId)
    {
        byte[] record = new byte[16];
        record[0] = hotbarNumber;
        record[1] = slotNumber;
        record[2] = (byte)kind;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4, 4), actionId);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(8, 4), classJobId);
        return record;
    }

    private static byte[] CreateKeybindDatBytes(IReadOnlyList<(string Command, string KeyString)> entries)
    {
        return CreateDatFileBytes(0x73, CreateKeybindContent(entries.ToArray()));
    }

    private static byte[] CreateAnchoredHotbarPayload(
        byte hotbarNumber,
        byte slotNumber,
        HotbarSlotKind kind,
        uint actionId,
        uint classJobId)
    {
        byte[] payload = new byte[15];
        payload[0] = hotbarNumber;
        payload[1] = slotNumber;
        payload[2] = (byte)kind;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4, 4), actionId);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8, 4), classJobId);
        return payload;
    }

    private static byte[] CreateKeybindContent(params (string Command, string KeyString)[] entries)
    {
        using var stream = new MemoryStream();

        foreach ((string command, string keyString) in entries)
        {
            WriteSection(stream, 'T', command);
            WriteSection(stream, 'C', keyString);
        }

        return stream.ToArray();
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

    private static byte[] CreateDatFileBytes(byte xorKey, byte[] payload)
    {
        byte[] fileBytes = new byte[0x11 + payload.Length + 1];

        uint headerMaxSize = (uint)(fileBytes.Length - 32);
        uint headerValidDataSize = (uint)((0x11 + payload.Length + 1) - 16);

        BinaryPrimitives.WriteUInt32LittleEndian(fileBytes.AsSpan(0x04, 4), headerMaxSize);
        BinaryPrimitives.WriteUInt32LittleEndian(fileBytes.AsSpan(0x08, 4), headerValidDataSize);

        for (int index = 0; index < payload.Length; index++)
        {
            fileBytes[0x11 + index] = (byte)(payload[index] ^ xorKey);
        }

        fileBytes[0x11 + payload.Length] = (byte)(0 ^ xorKey);
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
