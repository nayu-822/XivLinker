namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed class CharacterConfigFiles
{
    public required string CharacterDirectoryPath { get; init; }

    public required string HotbarPath { get; init; }

    public required string KeybindPath { get; init; }

    public required byte[] HotbarBytes { get; init; }

    public required byte[] KeybindBytes { get; init; }
}
