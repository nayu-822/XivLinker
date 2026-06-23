namespace XivLinker.Domain.Models.Crafting;

public sealed record CrafterActionVariant(
    uint LuminaRowId,
    uint IconId,
    uint? ClassJobRowId,
    string? ClassJobAbbreviation,
    string? ClassJobName);
