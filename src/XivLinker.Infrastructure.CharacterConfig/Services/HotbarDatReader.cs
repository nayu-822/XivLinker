using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Domain.Models;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class HotbarDatReader
{
    private const byte XorKey = 0x31;
    private readonly ILogger<HotbarDatReader> logger;

    public HotbarDatReader(ILogger<HotbarDatReader>? logger = null)
    {
        this.logger = logger ?? NullLogger<HotbarDatReader>.Instance;
    }

    public IReadOnlyList<HotbarSlotEntry> Read(
        byte[] hotbarDatBytes,
        CrafterJob crafterJob,
        IReadOnlySet<uint> knownCraftActionIds)
    {
        ArgumentNullException.ThrowIfNull(hotbarDatBytes);
        ArgumentNullException.ThrowIfNull(crafterJob);
        ArgumentNullException.ThrowIfNull(knownCraftActionIds);

        byte[] content = DatFileContentReader.ReadDecodedContent(
            hotbarDatBytes,
            xorKey: XorKey,
            fileName: "HOTBAR.DAT");

        logger.LogInformation(
            "HOTBAR.DAT decoded. DecodedLength: {Length}, FirstBytes: {FirstBytes}",
            content.Length,
            ToHex(content, 128));

        foreach (uint actionId in knownCraftActionIds)
        {
            IReadOnlyList<int> offsets = FindUInt32Offsets(content, actionId).Take(20).ToArray();

            logger.LogDebug(
                "HOTBAR.DAT action id occurrence diagnostic. ActionId: {ActionId}, Offsets: {Offsets}",
                actionId,
                string.Join(", ", offsets.Select(static offset => $"0x{offset:X}")));
        }

        IReadOnlyList<HotbarSlotEntry> entries = TryReadKnownLayout(content, crafterJob, knownCraftActionIds);
        if (entries.Count > 0)
        {
            foreach (HotbarSlotEntry slot in entries)
            {
                logger.LogInformation(
                    "Hotbar action resolved. Job: {Job}, Hotbar: {Hotbar}, Slot: {Slot}, ActionId: {ActionId}, Kind: {Kind}, ClassJobId: {ClassJobId}, Shared: {Shared}",
                    crafterJob.Name,
                    slot.HotbarNumber,
                    slot.SlotNumber,
                    slot.ActionOrCommandId,
                    slot.Kind,
                    slot.ClassJobId,
                    slot.IsShared);
            }

            return entries;
        }

        throw new UnsupportedCharacterConfigFormatException(
            "HOTBAR.DAT からシーケンス用アクションの登録情報を取得できませんでした。");
    }

    private IReadOnlyList<HotbarSlotEntry> TryReadKnownLayout(
        byte[] content,
        CrafterJob crafterJob,
        IReadOnlySet<uint> knownCraftActionIds)
    {
        HotbarRecordLayout[] layouts =
        [
            new(RecordSize: 16, HotbarOffset: 0, SlotOffset: 1, TypeOffset: 2, IdOffset: 4, ClassJobOffset: 8),
            new(RecordSize: 20, HotbarOffset: 0, SlotOffset: 1, TypeOffset: 2, IdOffset: 4, ClassJobOffset: 8),
            new(RecordSize: 24, HotbarOffset: 0, SlotOffset: 1, TypeOffset: 2, IdOffset: 4, ClassJobOffset: 8),
            new(RecordSize: 32, HotbarOffset: 0, SlotOffset: 1, TypeOffset: 2, IdOffset: 4, ClassJobOffset: 8),
        ];

        List<HotbarSlotEntry> bestEntries = [];
        HotbarRecordLayout? bestLayout = null;
        int bestKnownMatchCount = -1;
        int bestBaseOffset = 0;

        foreach (HotbarRecordLayout layout in layouts)
        {
            for (int baseOffset = 0; baseOffset < layout.RecordSize; baseOffset++)
            {
                List<HotbarSlotEntry> candidateEntries = TryReadLayout(content, baseOffset, layout, crafterJob);
                int knownMatchCount = candidateEntries.Count(entry => knownCraftActionIds.Contains(entry.ActionOrCommandId));

                logger.LogInformation(
                    "HOTBAR.DAT layout candidate parsed. Layout: {Layout}, BaseOffset: {BaseOffset}, EntryCount: {EntryCount}, KnownActionMatches: {KnownActionMatches}",
                    layout,
                    baseOffset,
                    candidateEntries.Count,
                    knownMatchCount);

                if (knownMatchCount > bestKnownMatchCount
                    || (knownMatchCount == bestKnownMatchCount && candidateEntries.Count > bestEntries.Count))
                {
                    bestEntries = candidateEntries;
                    bestLayout = layout;
                    bestKnownMatchCount = knownMatchCount;
                    bestBaseOffset = baseOffset;
                }
            }
        }

        if (knownCraftActionIds.Count > 0 && bestKnownMatchCount == 0)
        {
            logger.LogWarning(
                "HOTBAR.DAT could not locate any required craft action ids. RequiredActionIds: {RequiredActionIds}",
                string.Join(", ", knownCraftActionIds));

            throw new UnsupportedCharacterConfigFormatException(
                "HOTBAR.DAT からシーケンス用アクションの登録情報を取得できませんでした。");
        }

        if (bestEntries.Count > 0 && bestLayout is not null)
        {
            logger.LogInformation(
                "HOTBAR.DAT candidate parse matched. Layout: {Layout}, BaseOffset: {BaseOffset}, Entries: {Entries}",
                bestLayout,
                bestBaseOffset,
                string.Join(
                    ", ",
                    bestEntries.Select(static entry =>
                        $"hotbar={entry.HotbarNumber} slot={entry.SlotNumber} kind={entry.Kind} actionId={entry.ActionOrCommandId} classJob={entry.ClassJobId} shared={entry.IsShared}")));
        }

        return bestEntries;
    }

    private static List<HotbarSlotEntry> TryReadLayout(
        byte[] content,
        int baseOffset,
        HotbarRecordLayout layout,
        CrafterJob crafterJob)
    {
        var entries = new List<HotbarSlotEntry>();

        for (int offset = baseOffset; offset + layout.RecordSize <= content.Length; offset += layout.RecordSize)
        {
            byte hotbarNumber = content[offset + layout.HotbarOffset];
            byte slotNumber = content[offset + layout.SlotOffset];
            byte kindByte = content[offset + layout.TypeOffset];

            if (hotbarNumber is < 1 or > 10 || slotNumber is < 1 or > 12)
            {
                continue;
            }

            uint actionId = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(offset + layout.IdOffset, 4));
            if (actionId == 0)
            {
                continue;
            }

            HotbarSlotKind kind = ResolveKind(kindByte);
            if (kind is not HotbarSlotKind.Action and not HotbarSlotKind.GeneralAction)
            {
                continue;
            }

            uint? classJobId = null;
            bool isShared = true;

            if (offset + layout.ClassJobOffset + 4 <= content.Length)
            {
                uint rawClassJobId = BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(offset + layout.ClassJobOffset, 4));
                classJobId = rawClassJobId == 0 ? null : rawClassJobId;
                isShared = classJobId is null || classJobId == 0 || classJobId == crafterJob.ClassJobId;
            }

            entries.Add(new HotbarSlotEntry(
                hotbarNumber,
                slotNumber,
                kind,
                actionId,
                classJobId,
                isShared));
        }

        return entries
            .GroupBy(static entry => new { entry.HotbarNumber, entry.SlotNumber, entry.ActionOrCommandId })
            .Select(static group => group.First())
            .ToList();
    }

    private static IEnumerable<int> FindUInt32Offsets(byte[] content, uint value)
    {
        for (int offset = 0; offset + 4 <= content.Length; offset++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(offset, 4)) == value)
            {
                yield return offset;
            }
        }
    }

    private static HotbarSlotKind ResolveKind(byte value)
    {
        return value switch
        {
            0 => HotbarSlotKind.Empty,
            1 => HotbarSlotKind.Action,
            2 => HotbarSlotKind.GeneralAction,
            3 => HotbarSlotKind.Item,
            4 => HotbarSlotKind.Macro,
            _ => HotbarSlotKind.Unknown,
        };
    }

    private static string ToHex(byte[] bytes, int maxLength)
    {
        return Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, maxLength)));
    }

    private sealed record HotbarRecordLayout(
        int RecordSize,
        int HotbarOffset,
        int SlotOffset,
        int TypeOffset,
        int IdOffset,
        int ClassJobOffset);
}
