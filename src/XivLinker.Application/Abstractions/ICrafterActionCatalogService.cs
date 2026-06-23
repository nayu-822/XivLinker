using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Application.Abstractions;

public interface ICrafterActionCatalogService
{
    Task<CrafterActionCatalogResult> GetCrafterActionsAsync(CancellationToken cancellationToken = default);

    Task<byte[]?> GetIconPngAsync(uint iconId, CancellationToken cancellationToken = default);
}
