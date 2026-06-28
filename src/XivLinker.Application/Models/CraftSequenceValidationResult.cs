using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Application.Models;

public sealed class CraftSequenceValidationResult
{
    public bool CanRun => MissingActions.Count == 0;

    public IReadOnlyList<CraftActionRequirement> MissingActions { get; init; } = [];

    public string? Message { get; init; }
}

public sealed record CraftActionRequirement(
    CraftActionId ActionId,
    string ActionName);
