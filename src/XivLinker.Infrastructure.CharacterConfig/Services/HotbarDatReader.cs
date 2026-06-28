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
            "HOTBAR.DAT からホットバー登録情報を取得できませんでした。");
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

        foreach (HotbarRecordLayout layout in layouts)
        {
            List<HotbarSlotEntry> candidateEntries = TryReadLayout(content, layout, crafterJob, knownCraftActionIds);
            int knownMatchCount = candidateEntries.Count(entry => knownCraftActionIds.Contains(entry.ActionOrCommandId));

            if (knownMatchCount > bestKnownMatchCount
                || (knownMatchCount == bestKnownMatchCount && candidateEntries.Count > bestEntries.Count))
            {
                bestEntries = candidateEntries;
                bestLayout = layout;
                bestKnownMatchCount = knownMatchCount;
            }
        }

        if (bestEntries.Count > 0 && bestLayout is not null)
        {
            logger.LogInformation(
                "HOTBAR.DAT candidate parse matched. Layout: {Layout}, Entries: {Entries}",
                bestLayout,
                string.Join(
                    ", ",
                    bestEntries.Select(static entry =>
                        $"hotbar={entry.HotbarNumber} slot={entry.SlotNumber} kind={entry.Kind} actionId={entry.ActionOrCommandId} classJob={entry.ClassJobId} shared={entry.IsShared}")));
        }

        return bestEntries;
    }

    private static List<HotbarSlotEntry> TryReadLayout(
        byte[] content,
        HotbarRecordLayout layout,
        CrafterJob crafterJob,
        IReadOnlySet<uint> knownCraftActionIds)
    {
        var entries = new List<HotbarSlotEntry>();

        for (int offset = 0; offset + layout.RecordSize <= content.Length; offset += layout.RecordSize)
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
                isShared = classJobId is null || classJobId == crafterJob.ClassJobId;
            }

            entries.Add(new HotbarSlotEntry(
                hotbarNumber,
                slotNumber,
                HotbarSlotKind.Action,
                actionId,
                classJobId,
                isShared));
        }

        return entries
            .GroupBy(static entry => new { entry.HotbarNumber, entry.SlotNumber, entry.ActionOrCommandId })
            .Select(static group => group.First())
            .ToList();
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

    private sealed record HotbarRecordLayout(
        int RecordSize,
        int HotbarOffset,
        int SlotOffset,
        int TypeOffset,
        int IdOffset,
        int ClassJobOffset);
}
