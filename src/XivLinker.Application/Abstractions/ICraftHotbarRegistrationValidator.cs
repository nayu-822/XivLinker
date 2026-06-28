using XivLinker.Application.Models;
using XivLinker.Domain.Models;

namespace XivLinker.Application.Abstractions;

public interface ICraftHotbarRegistrationValidator
{
    Task<CraftSequenceValidationResult> ValidateAsync(
        CraftSequence sequence,
        CrafterJob crafterJob,
        CancellationToken cancellationToken = default);
}
