using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Application.Models;

public sealed class CraftSequenceExecutionPreparationResult
{
    public bool CanRun =>
        string.IsNullOrWhiteSpace(ErrorMessage)
        && MissingActions.Count == 0
        && UnboundActions.Count == 0
        && ActionKeyBindings.Count > 0;

    public IReadOnlyList<CraftActionRequirement> MissingActions { get; init; } = [];

    public IReadOnlyList<CraftActionRequirement> UnboundActions { get; init; } = [];

    public IReadOnlyList<CraftActionKeyBinding> ActionKeyBindings { get; init; } = [];

    public string? ErrorMessage { get; init; }

    public static CraftSequenceExecutionPreparationResult Failed(string message)
    {
        return new CraftSequenceExecutionPreparationResult
        {
            ErrorMessage = message,
        };
    }
}

public sealed record CraftActionRequirement(
    CraftActionId ActionId,
    uint LuminaActionId,
    string ActionName);

public sealed record CraftActionKeyBinding(
    CraftActionId ActionId,
    string ActionName,
    int HotbarNumber,
    int SlotNumber,
    string KeyGestureText,
    IReadOnlyList<string> Keys);
