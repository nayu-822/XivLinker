using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Domain.Models;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class HotbarDatReader
{
    private const int HeaderSize = 16;
    private const int RecordSize = 8;
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

        if (hotbarDatBytes.Length < HeaderSize)
        {
            throw new InvalidDataException("HOTBAR.DAT のサイズが不正です。");
        }

        byte[] decodedBody = DecodeBody(hotbarDatBytes.AsSpan(HeaderSize).ToArray(), XorKey);
        if (decodedBody.Length % RecordSize != 0)
        {
            throw new InvalidDataException("HOTBAR.DAT の body が 8 byte record に揃っていません。");
        }

        logger.LogInformation(
            "HOTBAR.DAT decoded. DecodedLength: {Length}, FirstBytes: {FirstBytes}",
            decodedBody.Length,
            Convert.ToHexString(decodedBody.AsSpan(0, Math.Min(decodedBody.Length, 128))));

        var entries = new List<HotbarSlotEntry>();

        for (int offset = 0; offset < decodedBody.Length; offset += RecordSize)
        {
            var entry = new HotbarSlotEntry(
                BinaryPrimitives.ReadUInt32LittleEndian(decodedBody.AsSpan(offset, 4)),
                decodedBody[offset + 4],
                decodedBody[offset + 5],
                decodedBody[offset + 6],
                decodedBody[offset + 7]);

            entries.Add(entry);

            if (knownCraftActionIds.Contains(entry.CommandId))
            {
                logger.LogInformation(
                    "HOTBAR craft action found. CommandId: {CommandId}, GroupId: {GroupId}, HotbarId: {HotbarId}, SlotId: {SlotId}, SlotTypeId: {SlotTypeId}",
                    entry.CommandId,
                    entry.GroupId,
                    entry.HotbarId,
                    entry.SlotId,
                    entry.SlotTypeId);
            }
        }

        foreach (uint actionId in knownCraftActionIds)
        {
            IReadOnlyList<HotbarSlotEntry> matches = entries
                .Where(entry => entry.CommandId == actionId)
                .ToArray();

            logger.LogInformation(
                "HOTBAR.DAT CommandId occurrence. CommandId: {CommandId}, Matches: {Matches}",
                actionId,
                matches.Count == 0
                    ? "-"
                    : string.Join(", ", matches.Select(static item =>
                        $"group={item.GroupId} hotbar={item.HotbarId} slot={item.SlotId} type={item.SlotTypeId}")));
        }

        if (knownCraftActionIds.Count > 0 && !entries.Any(entry => knownCraftActionIds.Contains(entry.CommandId)))
        {
            throw new UnsupportedCharacterConfigFormatException(
                "HOTBAR.DAT からシーケンス用アクションの登録情報を取得できませんでした。");
        }

        return entries;
    }

    private static byte[] DecodeBody(byte[] encodedBody, byte xorKey)
    {
        byte[] decodedBody = new byte[encodedBody.Length];

        for (int index = 0; index < encodedBody.Length; index++)
        {
            decodedBody[index] = (byte)(encodedBody[index] ^ xorKey);
        }

        return decodedBody;
    }
}
