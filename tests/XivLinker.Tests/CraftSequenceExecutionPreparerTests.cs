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
    public async Task PrepareAsync_ReturnsBindings_WhenActionsAreRegisteredAndBound()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteHotbarFile(rootPath, [CreateHotbarEntry(1, 1, HotbarSlotKind.Action, actionRowId, CrafterJobs.Carpenter.ClassJobId, false)]);
            WriteKeybindFile(rootPath, [new HotbarSlotKeyBinding(1, 1, "1", ["1"])]);

            var preparer = CreatePreparer(rootPath);

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
    public async Task PrepareAsync_ReturnsMissingActionsOnlyOnce()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint synthesisRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteHotbarFile(rootPath, [CreateHotbarEntry(1, 1, HotbarSlotKind.Action, synthesisRowId, CrafterJobs.Carpenter.ClassJobId, false)]);
            WriteKeybindFile(rootPath, [new HotbarSlotKeyBinding(1, 1, "1", ["1"])]);

            var preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result = await preparer.PrepareAsync(
                new CraftSequence
                {
                    Steps =
                    [
                        new CraftSequenceStep { ActionId = CraftActionId.BasicSynthesis },
                        new CraftSequenceStep { ActionId = CraftActionId.BasicTouch },
                        new CraftSequenceStep { ActionId = CraftActionId.BasicTouch },
                    ],
                },
                CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            CraftActionRequirement missing = Assert.Single(result.MissingActions);
            Assert.Equal(CraftActionId.BasicTouch, missing.ActionId);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReturnsUnboundActions_WhenKeyBindingIsMissing()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteHotbarFile(rootPath, [CreateHotbarEntry(1, 1, HotbarSlotKind.Action, actionRowId, CrafterJobs.Carpenter.ClassJobId, false)]);
            WriteKeybindFile(rootPath, []);

            var preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.Single(result.UnboundActions);
            Assert.Empty(result.ActionKeyBindings);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_ReloadsLatestFiles_OnEachCall()
    {
        string rootPath = CreateTempDirectory();

        try
        {
            uint actionRowId = GetLuminaActionId(CraftActionId.BasicSynthesis, CrafterJobs.Carpenter);
            WriteHotbarFile(rootPath, []);
            WriteKeybindFile(rootPath, []);

            var preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult first =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            WriteHotbarFile(rootPath, [CreateHotbarEntry(1, 1, HotbarSlotKind.Action, actionRowId, CrafterJobs.Carpenter.ClassJobId, false)]);
            WriteKeybindFile(rootPath, [new HotbarSlotKeyBinding(1, 1, "1", ["1"])]);

            CraftSequenceExecutionPreparationResult second =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(first.CanRun);
            Assert.Single(first.MissingActions);
            Assert.True(second.CanRun);
            Assert.Single(second.ActionKeyBindings);
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
            var preparer = CreatePreparer(rootPath);

            CraftSequenceExecutionPreparationResult result =
                await preparer.PrepareAsync(CreateSequence(CraftActionId.BasicSynthesis), CrafterJobs.Carpenter);

            Assert.False(result.CanRun);
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
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

    private static uint GetLuminaActionId(CraftActionId actionId, CrafterJob crafterJob)
    {
        CraftActionDefinition definition = CraftActionCatalog.Get(actionId);
        CrafterActionVariant variant = definition.Variants.First(item => item.ClassJobRowId == crafterJob.ClassJobId);
        return variant.LuminaRowId;
    }

    private static HotbarSlotEntry CreateHotbarEntry(
        int hotbarNumber,
        int slotNumber,
        HotbarSlotKind kind,
        uint actionOrCommandId,
        uint classJobId,
        bool isShared)
    {
        return new HotbarSlotEntry(
            hotbarNumber,
            slotNumber,
            kind,
            actionOrCommandId,
            classJobId,
            isShared);
    }

    private static void WriteHotbarFile(string characterDirectoryPath, IReadOnlyList<HotbarSlotEntry> entries)
    {
        File.WriteAllBytes(
            Path.Combine(characterDirectoryPath, "HOTBAR.DAT"),
            CreateEncodedHotbarBytes(entries));
    }

    private static void WriteKeybindFile(string characterDirectoryPath, IReadOnlyList<HotbarSlotKeyBinding> bindings)
    {
        File.WriteAllBytes(
            Path.Combine(characterDirectoryPath, "KEYBIND.DAT"),
            CreateEncodedKeybindBytes(bindings));
    }

    private static byte[] CreateEncodedHotbarBytes(IReadOnlyList<HotbarSlotEntry> entries)
    {
        byte[] decoded = new byte[8 + (entries.Count * 24)];
        Encoding.ASCII.GetBytes("XHB1").CopyTo(decoded, 0);
        BinaryPrimitives.WriteInt32LittleEndian(decoded.AsSpan(4, 4), entries.Count);

        int offset = 8;
        foreach (HotbarSlotEntry entry in entries)
        {
            BinaryPrimitives.WriteInt32LittleEndian(decoded.AsSpan(offset, 4), entry.HotbarNumber);
            BinaryPrimitives.WriteInt32LittleEndian(decoded.AsSpan(offset + 4, 4), entry.SlotNumber);
            BinaryPrimitives.WriteInt32LittleEndian(decoded.AsSpan(offset + 8, 4), (int)entry.Kind);
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(offset + 12, 4), entry.ActionOrCommandId);
            BinaryPrimitives.WriteUInt32LittleEndian(decoded.AsSpan(offset + 16, 4), entry.ClassJobId ?? 0);
            BinaryPrimitives.WriteInt32LittleEndian(decoded.AsSpan(offset + 20, 4), entry.IsShared ? 1 : 0);
            offset += 24;
        }

        return Xor(decoded, 0x31);
    }

    private static byte[] CreateEncodedKeybindBytes(IReadOnlyList<HotbarSlotKeyBinding> bindings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("XKB1"));
        writer.Write(bindings.Count);

        foreach (HotbarSlotKeyBinding binding in bindings)
        {
            writer.Write(binding.HotbarNumber);
            writer.Write(binding.SlotNumber);
            WriteString(writer, binding.KeyGestureText);
            writer.Write(binding.Keys.Count);

            foreach (string key in binding.Keys)
            {
                WriteString(writer, key);
            }
        }

        writer.Flush();
        return Xor(stream.ToArray(), 0x73);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static byte[] Xor(byte[] source, byte key)
    {
        byte[] bytes = new byte[source.Length];

        for (int index = 0; index < source.Length; index++)
        {
            bytes[index] = (byte)(source[index] ^ key);
        }

        return bytes;
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
