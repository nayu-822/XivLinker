using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Application.Models;

public sealed class CraftSequenceValidationResult
{
    public bool CanRun => MissingActions.Count == 0 && string.IsNullOrWhiteSpace(ErrorMessage);

    public IReadOnlyList<CraftActionRequirement> MissingActions { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public static CraftSequenceValidationResult Failed(string message)
    {
        return new CraftSequenceValidationResult
        {
            ErrorMessage = message,
        };
    }
}

public sealed record CraftActionRequirement(
    CraftActionId ActionId,
    string ActionName);
