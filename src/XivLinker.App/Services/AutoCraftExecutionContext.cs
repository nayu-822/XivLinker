using XivLinker.Application.Models;
using XivLinker.Domain.Models;

namespace XivLinker.App.Services;

public sealed class AutoCraftExecutionContext
{
    public required CraftSequence Sequence { get; init; }

    public int RunCount { get; init; }

    public required CrafterJob CrafterJob { get; init; }

    public required IReadOnlyList<CraftActionKeyBinding> ActionKeyBindings { get; init; }
}
