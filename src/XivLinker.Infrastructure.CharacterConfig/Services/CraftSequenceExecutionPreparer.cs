using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CraftSequenceExecutionPreparer : ICraftSequenceExecutionPreparer
{
    private readonly CharacterConfigFileLoader characterConfigFileLoader;
    private readonly HotbarDatReader hotbarDatReader;
    private readonly KeybindDatReader keybindDatReader;
    private readonly ILogger<CraftSequenceExecutionPreparer> logger;

    public CraftSequenceExecutionPreparer(
        CharacterConfigFileLoader characterConfigFileLoader,
        HotbarDatReader hotbarDatReader,
        KeybindDatReader keybindDatReader,
        ILogger<CraftSequenceExecutionPreparer> logger)
    {
        this.characterConfigFileLoader = characterConfigFileLoader;
        this.hotbarDatReader = hotbarDatReader;
        this.keybindDatReader = keybindDatReader;
        this.logger = logger;
    }

    public async Task<CraftSequenceExecutionPreparationResult> PrepareAsync(
        CraftSequence sequence,
        CrafterJob crafterJob,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CraftActionRequirement> requiredActions = ResolveRequiredActions(sequence, crafterJob);
        if (requiredActions.Count == 0)
        {
            return CraftSequenceExecutionPreparationResult.Failed(
                "シーケンスに実行可能なクラフターアクションが含まれていません。");
        }

        logger.LogInformation(
            "Craft execution preparation started. Sequence: {SequenceName}, CrafterJob: {CrafterJobName}, RequiredActions: {RequiredActions}",
            sequence.Name,
            crafterJob.Name,
            string.Join(", ", requiredActions.Select(static action => action.ActionName)));

        CharacterConfigFiles files;
        try
        {
            files = await characterConfigFileLoader.LoadLatestAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Failed to load HOTBAR.DAT / KEYBIND.DAT for craft execution preparation.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "HOTBAR.DAT または KEYBIND.DAT を読み込めないため、シーケンスを実行できません。");
        }

        IReadOnlyList<HotbarSlotEntry> hotbarSlots;
        IReadOnlyList<HotbarSlotKeyBinding> keyBindings;

        try
        {
            hotbarSlots = hotbarDatReader.Read(files.HotbarBytes, crafterJob);
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(exception, "Failed to parse HOTBAR.DAT for craft execution preparation.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "HOTBAR.DAT を解釈できないため、シーケンスを実行できません。");
        }

        try
        {
            keyBindings = keybindDatReader.Read(files.KeybindBytes);
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(exception, "Failed to parse KEYBIND.DAT for craft execution preparation.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "KEYBIND.DAT を解釈できないため、シーケンスを実行できません。");
        }

        var actionKeyBindings = new List<CraftActionKeyBinding>();
        var missingActions = new List<CraftActionRequirement>();
        var unboundActions = new List<CraftActionRequirement>();

        foreach (CraftActionRequirement requiredAction in requiredActions)
        {
            HotbarSlotEntry? slot = hotbarSlots.FirstOrDefault(candidate =>
                candidate.Kind == HotbarSlotKind.Action
                && candidate.ActionOrCommandId == requiredAction.LuminaActionId);

            if (slot is null)
            {
                missingActions.Add(requiredAction);
                continue;
            }

            HotbarSlotKeyBinding? keyBinding = keyBindings.FirstOrDefault(candidate =>
                candidate.HotbarNumber == slot.HotbarNumber
                && candidate.SlotNumber == slot.SlotNumber);

            if (keyBinding is null || keyBinding.Keys.Count == 0)
            {
                unboundActions.Add(requiredAction);
                continue;
            }

            actionKeyBindings.Add(new CraftActionKeyBinding(
                requiredAction.ActionId,
                requiredAction.ActionName,
                slot.HotbarNumber,
                slot.SlotNumber,
                keyBinding.KeyGestureText,
                keyBinding.Keys));
        }

        logger.LogInformation(
            "Craft execution preparation completed. Sequence: {SequenceName}, ResolvedBindings: {ResolvedBindings}, MissingActions: {MissingActions}, UnboundActions: {UnboundActions}",
            sequence.Name,
            string.Join(", ", actionKeyBindings.Select(static binding => $"{binding.ActionName} -> Hotbar {binding.HotbarNumber} Slot {binding.SlotNumber} -> {binding.KeyGestureText}")),
            string.Join(", ", missingActions.Select(static action => action.ActionName)),
            string.Join(", ", unboundActions.Select(static action => action.ActionName)));

        return new CraftSequenceExecutionPreparationResult
        {
            MissingActions = missingActions,
            UnboundActions = unboundActions,
            ActionKeyBindings = actionKeyBindings,
        };
    }

    private static IReadOnlyList<CraftActionRequirement> ResolveRequiredActions(CraftSequence sequence, CrafterJob crafterJob)
    {
        return sequence.Steps
            .Select(step => step.ActionId)
            .Where(actionId => !string.IsNullOrWhiteSpace(actionId.Value))
            .Distinct()
            .Select(actionId =>
            {
                if (!CraftActionCatalog.TryGet(actionId, out CraftActionDefinition? definition) || definition is null)
                {
                    return null;
                }

                CrafterActionVariant? variant = definition.Variants
                    .FirstOrDefault(item => item.ClassJobRowId == crafterJob.ClassJobId);

                return variant is null
                    ? null
                    : new CraftActionRequirement(actionId, variant.LuminaRowId, definition.DisplayName);
            })
            .Where(static item => item is not null)
            .Cast<CraftActionRequirement>()
            .ToArray();
    }
}
