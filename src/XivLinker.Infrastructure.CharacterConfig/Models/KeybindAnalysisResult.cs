namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed class KeybindAnalysisResult
{
    public required string FilePath { get; init; }

    public required bool Exists { get; init; }

    public long ByteLength { get; init; }

    public DateTimeOffset? LastWriteTime { get; init; }

    public byte[]? RawBytes { get; init; }
}
