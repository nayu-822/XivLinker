using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class KeybindDatReader
{
    private const byte XorKey = 0x73;
    private readonly ILogger<KeybindDatReader> logger;

    public KeybindDatReader(ILogger<KeybindDatReader>? logger = null)
    {
        this.logger = logger ?? NullLogger<KeybindDatReader>.Instance;
    }

    public IReadOnlyList<HotbarSlotKeyBinding> Read(byte[] encodedBytes)
    {
        ArgumentNullException.ThrowIfNull(encodedBytes);

        byte[] xorDecodedBytes = DecodeWithXor(encodedBytes, XorKey);

        logger.LogWarning(
            "KEYBIND.DAT format is not supported. Length: {Length}, FirstBytes: {FirstBytes}, Xor73FirstBytes: {DecodedFirstBytes}",
            encodedBytes.Length,
            ToHex(encodedBytes, 32),
            ToHex(xorDecodedBytes, 32));

        throw new UnsupportedCharacterConfigFormatException(
            "KEYBIND.DAT の実ファイル形式にまだ対応できていないため、シーケンスを準備できません。");
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

    private static string ToHex(byte[] bytes, int maxLength)
    {
        return Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, maxLength)));
    }
}
