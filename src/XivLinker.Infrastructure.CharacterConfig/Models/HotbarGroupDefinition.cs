namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record HotbarGroupDefinition(
    byte GroupId,
    uint? ClassJobId,
    string Name);
