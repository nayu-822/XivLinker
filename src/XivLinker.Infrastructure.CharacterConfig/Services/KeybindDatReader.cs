using System.Buffers.Binary;
using System.Text;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class KeybindDatReader
{
    private const byte XorKey = 0x73;
    private const string Magic = "XKB1";

    public IReadOnlyList<HotbarSlotKeyBinding> Read(byte[] encodedBytes)
    {
        byte[] bytes = DecodeWithXor(encodedBytes, XorKey);
        if (bytes.Length < 8 || Encoding.ASCII.GetString(bytes, 0, 4) != Magic)
        {
            throw new InvalidDataException("KEYBIND.DAT のフォーマットを解釈できません。");
        }

        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        int offset = 8;
        var bindings = new List<HotbarSlotKeyBinding>(count);

        for (int index = 0; index < count; index++)
        {
            int hotbarNumber = ReadInt32(bytes, ref offset);
            int slotNumber = ReadInt32(bytes, ref offset);
            string keyGestureText = ReadString(bytes, ref offset);
            int keyCount = ReadInt32(bytes, ref offset);
            var keys = new List<string>(keyCount);

            for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
            {
                keys.Add(ReadString(bytes, ref offset));
            }

            bindings.Add(new HotbarSlotKeyBinding(
                hotbarNumber,
                slotNumber,
                keyGestureText,
                keys));
        }

        return bindings;
    }

    private static int ReadInt32(byte[] bytes, ref int offset)
    {
        if (offset + 4 > bytes.Length)
        {
            throw new InvalidDataException("KEYBIND.DAT の整数データが不足しています。");
        }

        int value = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    private static string ReadString(byte[] bytes, ref int offset)
    {
        int length = ReadInt32(bytes, ref offset);
        if (length < 0 || offset + length > bytes.Length)
        {
            throw new InvalidDataException("KEYBIND.DAT の文字列データが壊れています。");
        }

        string value = Encoding.UTF8.GetString(bytes, offset, length);
        offset += length;
        return value;
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
