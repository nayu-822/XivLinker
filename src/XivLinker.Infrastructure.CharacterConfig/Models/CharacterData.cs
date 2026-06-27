namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed class CharacterData
{
    public required CharacterProfile Profile { get; init; }

    public required HotbarAnalysisResult HotbarAnalysisResult { get; init; }

    public required KeybindAnalysisResult KeybindAnalysisResult { get; init; }

    public required DateTimeOffset LoadedAt { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }
}
