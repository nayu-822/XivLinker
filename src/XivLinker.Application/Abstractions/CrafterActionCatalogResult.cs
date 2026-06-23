using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Application.Abstractions;

public sealed record CrafterActionCatalogResult(
    IReadOnlyList<CraftActionDefinition> Actions,
    string? ErrorMessage = null)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);
}
