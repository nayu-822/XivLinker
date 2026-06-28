using System.Buffers.Binary;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

internal static class DatFileContentReader
{
    private const int HeaderSize = 0x11;
    private const int MaxSizeOffset = 32;
    private const int ContentSizeOffset = 16;

    public static byte[] ReadDecodedContent(
        byte[] fileBytes,
        byte xorKey,
        string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (fileBytes.Length < HeaderSize)
        {
            throw new InvalidDataException($"{fileName} のヘッダーが短すぎます。");
        }

        uint headerMaxSize = BinaryPrimitives.ReadUInt32LittleEndian(fileBytes.AsSpan(0x04, 4));
        uint headerValidDataSize = BinaryPrimitives.ReadUInt32LittleEndian(fileBytes.AsSpan(0x08, 4));

        int expectedFileSize;
        int validDataEndOffset;
        try
        {
            expectedFileSize = checked((int)headerMaxSize + MaxSizeOffset);
            validDataEndOffset = checked((int)headerValidDataSize + ContentSizeOffset);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"{fileName} のヘッダー値が不正です。", exception);
        }

        if (expectedFileSize != fileBytes.Length)
        {
            throw new InvalidDataException(
                $"{fileName} のファイルサイズがヘッダーと一致しません。expected={expectedFileSize}, actual={fileBytes.Length}");
        }

        if (validDataEndOffset <= HeaderSize || validDataEndOffset > fileBytes.Length)
        {
            throw new InvalidDataException(
                $"{fileName} の有効データサイズが不正です。validDataEndOffset={validDataEndOffset}, fileSize={fileBytes.Length}");
        }

        int decodedContentLength = validDataEndOffset - HeaderSize;
        byte[] content = fileBytes
            .AsSpan(HeaderSize, decodedContentLength)
            .ToArray();

        for (int index = 0; index < content.Length; index++)
        {
            content[index] ^= xorKey;
        }

        if (content.Length > 0 && content[^1] == 0)
        {
            Array.Resize(ref content, content.Length - 1);
        }

        return content;
    }
}
