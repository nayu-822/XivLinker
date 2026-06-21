namespace XivLinker.Domain.Models.Crafting;

public sealed record CraftActionDefinition(
    CraftActionId ActionId,
    string DisplayName,
    int PostActionWaitMilliseconds,
    string Category);
