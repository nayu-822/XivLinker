using System.Buffers.Binary;
using System.Text;
using XivLinker.Domain.Models;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class HotbarDatReader
{
    private const byte XorKey = 0x31;
    private const string Magic = "XHB1";

    public IReadOnlyList<HotbarSlotEntry> Read(byte[] encodedBytes, CrafterJob crafterJob)
    {
        byte[] bytes = DecodeWithXor(encodedBytes, XorKey);
        if (bytes.Length < 8 || Encoding.ASCII.GetString(bytes, 0, 4) != Magic)
        {
            throw new InvalidDataException("HOTBAR.DAT のフォーマットを解釈できません。");
        }

        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        int offset = 8;
        var entries = new List<HotbarSlotEntry>(count);

        for (int index = 0; index < count; index++)
        {
            if (offset + 24 > bytes.Length)
            {
                throw new InvalidDataException("HOTBAR.DAT のスロット情報が途中で終了しています。");
            }

            int hotbarNumber = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
            int slotNumber = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            int kindValue = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 8, 4));
            uint actionOrCommandId = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 12, 4));
            uint classJobId = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 16, 4));
            bool isShared = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 20, 4)) != 0;
            offset += 24;

            uint? scopedClassJobId = classJobId == 0 ? null : classJobId;
            if (!isShared && scopedClassJobId is not null && scopedClassJobId != crafterJob.ClassJobId)
            {
                continue;
            }

            entries.Add(new HotbarSlotEntry(
                hotbarNumber,
                slotNumber,
                ResolveKind(kindValue),
                actionOrCommandId,
                scopedClassJobId,
                isShared));
        }

        return entries;
    }

    private static HotbarSlotKind ResolveKind(int value)
    {
        return Enum.IsDefined(typeof(HotbarSlotKind), value)
            ? (HotbarSlotKind)value
            : HotbarSlotKind.Unknown;
    }

    private static byte[] DecodeWithXor(byte[] source, byte key)
    {
        byte[] decoded = new byte[source.Length];

        for (int index = 0; index < source.Length; index++)
        {
            decoded[index] = (byte)(source[index] ^ key);
        }

        return decoded;
    }
}
