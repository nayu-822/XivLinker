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

    public IReadOnlyList<HotbarSlotEntry> Read(byte[] encodedBytes, CrafterJob crafterJob)
    {
        ArgumentNullException.ThrowIfNull(encodedBytes);
        ArgumentNullException.ThrowIfNull(crafterJob);

        byte[] xorDecodedBytes = DecodeWithXor(encodedBytes, XorKey);

        logger.LogWarning(
            "HOTBAR.DAT format is not supported. Length: {Length}, FirstBytes: {FirstBytes}, Xor31FirstBytes: {DecodedFirstBytes}",
            encodedBytes.Length,
            ToHex(encodedBytes, 32),
            ToHex(xorDecodedBytes, 32));

        throw new UnsupportedCharacterConfigFormatException(
            "HOTBAR.DAT の実ファイル形式にまだ対応できていないため、シーケンスを準備できません。");
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
