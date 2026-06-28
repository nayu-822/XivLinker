using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Domain.Models;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CraftSequenceExecutionPreparer : ICraftSequenceExecutionPreparer
{
    private readonly ICraftActionIdResolver craftActionIdResolver;
    private readonly CharacterConfigFileLoader characterConfigFileLoader;
    private readonly HotbarDatReader hotbarDatReader;
    private readonly KeybindDatReader keybindDatReader;
    private readonly ILogger<CraftSequenceExecutionPreparer> logger;

    public CraftSequenceExecutionPreparer(
        ICraftActionIdResolver craftActionIdResolver,
        CharacterConfigFileLoader characterConfigFileLoader,
        HotbarDatReader hotbarDatReader,
        KeybindDatReader keybindDatReader,
        ILogger<CraftSequenceExecutionPreparer> logger)
    {
        this.craftActionIdResolver = craftActionIdResolver;
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
        IReadOnlyList<CraftActionRequirement> requiredActions = await craftActionIdResolver.ResolveRequiredActionsAsync(
            sequence,
            crafterJob,
            cancellationToken);

        if (requiredActions.Count == 0)
        {
            return CraftSequenceExecutionPreparationResult.Failed(
                "シーケンスに実行対象のクラフトアクションが含まれていないため、準備できません。");
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
                "HOTBAR.DAT または KEYBIND.DAT を読み込めないため、シーケンスを準備できません。");
        }

        IReadOnlyList<HotbarSlotEntry> hotbarSlots;
        try
        {
            hotbarSlots = hotbarDatReader.Read(files.HotbarBytes);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            logger.LogWarning(exception, "Failed to parse HOTBAR.DAT.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "HOTBAR.DAT を読み込めませんでした。");
        }

        IReadOnlyList<KeybindEntry> keybindEntries;
        try
        {
            keybindEntries = keybindDatReader.Read(files.KeybindBytes);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            logger.LogWarning(
                exception,
                "Failed to parse KEYBIND.DAT. Message: {Message}",
                exception.Message);
            return CraftSequenceExecutionPreparationResult.Failed(
                "KEYBIND.DAT を読み込めませんでした。");
        }

        var missingActions = new List<CraftActionRequirement>();
        var unboundActions = new List<CraftActionRequirement>();
        var actionKeyBindings = new List<CraftActionKeyBinding>();

        foreach (CraftActionRequirement requiredAction in requiredActions)
        {
            HotbarSlotEntry? slot = hotbarSlots.FirstOrDefault(slot =>
                slot.CommandId == requiredAction.LuminaActionId
                && IsAvailableForCrafterJob(slot, crafterJob));

            if (slot is null)
            {
                missingActions.Add(requiredAction);
                continue;
            }

            logger.LogDebug(
                "Craft action hotbar slot resolved. Action: {Action}, LuminaActionId: {LuminaActionId}, GroupId: {GroupId}, HotbarId: {HotbarId}, SlotId: {SlotId}, SlotTypeId: {SlotTypeId}",
                requiredAction.ActionName,
                requiredAction.LuminaActionId,
                slot.GroupId,
                slot.HotbarId,
                slot.SlotId,
                slot.SlotTypeId);

            KeybindEntry? keybind = ResolveKeybindForHotbarSlot(keybindEntries, slot.HotbarId, slot.SlotId);
            if (keybind is null)
            {
                unboundActions.Add(requiredAction);
                continue;
            }

            KeybindGesture? gesture = keybind.Primary ?? keybind.Secondary;
            if (gesture is null || !gesture.IsAssigned)
            {
                unboundActions.Add(requiredAction);
                continue;
            }

            actionKeyBindings.Add(new CraftActionKeyBinding(
                requiredAction.ActionId,
                requiredAction.ActionName,
                slot.HotbarId,
                slot.SlotId,
                KeybindDisplayFormatter.Format(gesture),
                KeybindDisplayFormatter.ToKeys(gesture)));
        }

        var result = new CraftSequenceExecutionPreparationResult
        {
            MissingActions = missingActions,
            UnboundActions = unboundActions,
            ActionKeyBindings = actionKeyBindings,
        };

        logger.LogInformation(
            "Craft execution preparation completed. Bindings: {BindingCount}, MissingActions: {MissingCount}, UnboundActions: {UnboundCount}",
            result.ActionKeyBindings.Count,
            result.MissingActions.Count,
            result.UnboundActions.Count);

        return result;
    }

    private static bool IsAvailableForCrafterJob(HotbarSlotEntry slot, CrafterJob crafterJob)
    {
        if (HotbarGroupDefinitions.IsShared(slot.GroupId))
        {
            return true;
        }

        return HotbarGroupDefinitions.TryGetDefinition(slot.GroupId, out HotbarGroupDefinition? definition)
            && definition is not null
            && definition.ClassJobId == crafterJob.ClassJobId;
    }

    private KeybindEntry? ResolveKeybindForHotbarSlot(
        IReadOnlyList<KeybindEntry> keybindEntries,
        byte hotbarId,
        byte slotId)
    {
        foreach (KeybindEntry entry in keybindEntries)
        {
            if (!KeybindDatReader.TryResolveHotbarCommand(entry.Command, out byte resolvedHotbarId, out byte resolvedSlotId))
            {
                continue;
            }

            logger.LogDebug(
                "KEYBIND hotbar command resolved. Command: {Command}, ResolvedHotbarId: {ResolvedHotbarId}, ResolvedSlotId: {ResolvedSlotId}, Primary: {Primary}, Secondary: {Secondary}",
                entry.Command,
                resolvedHotbarId,
                resolvedSlotId,
                KeybindDisplayFormatter.Format(entry.Primary),
                KeybindDisplayFormatter.Format(entry.Secondary));

            if (resolvedHotbarId == hotbarId && resolvedSlotId == slotId)
            {
                return entry;
            }
        }

        logger.LogWarning(
            "No KEYBIND entry matched HOTBAR slot. HotbarId: {HotbarId}, SlotId: {SlotId}",
            hotbarId,
            slotId);

        return null;
    }
}
