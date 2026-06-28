using XivLinker.Application.Models;
using XivLinker.Domain.Models;

namespace XivLinker.Application.Abstractions;

public interface ICraftActionIdResolver
{
    Task<IReadOnlyList<CraftActionRequirement>> ResolveRequiredActionsAsync(
        CraftSequence sequence,
        CrafterJob crafterJob,
        CancellationToken cancellationToken = default);
}
