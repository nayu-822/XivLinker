using XivLinker.Application.Models;
using XivLinker.Domain.Models;

namespace XivLinker.Application.Abstractions;

public interface ICraftSequenceExecutionPreparer
{
    Task<CraftSequenceExecutionPreparationResult> PrepareAsync(
        CraftSequence sequence,
        CrafterJob crafterJob,
        CancellationToken cancellationToken = default);
}
