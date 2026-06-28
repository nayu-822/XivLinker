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
        uint headerContentSize = BinaryPrimitives.ReadUInt32LittleEndian(fileBytes.AsSpan(0x08, 4));

        int expectedFileSize = checked((int)headerMaxSize + MaxSizeOffset);
        int contentSize = checked((int)headerContentSize + ContentSizeOffset);

        if (expectedFileSize != fileBytes.Length)
        {
            throw new InvalidDataException(
                $"{fileName} のファイルサイズがヘッダーと一致しません。expected={expectedFileSize}, actual={fileBytes.Length}");
        }

        if (contentSize <= 0 || HeaderSize + contentSize > fileBytes.Length + 1)
        {
            throw new InvalidDataException(
                $"{fileName} のcontent sizeが不正です。contentSize={contentSize}, fileSize={fileBytes.Length}");
        }

        byte[] content = fileBytes
            .AsSpan(HeaderSize, contentSize - 1)
            .ToArray();

        for (int index = 0; index < content.Length; index++)
        {
            content[index] ^= xorKey;
        }

        return content;
    }
}
