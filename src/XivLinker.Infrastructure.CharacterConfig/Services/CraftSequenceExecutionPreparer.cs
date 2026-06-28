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

        logger.LogInformation(
            "Character config files loaded. HotbarPath: {HotbarPath}, HotbarBytes: {HotbarLength}, KeybindPath: {KeybindPath}, KeybindBytes: {KeybindLength}",
            files.HotbarPath,
            files.HotbarBytes.Length,
            files.KeybindPath,
            files.KeybindBytes.Length);

        IReadOnlySet<uint> knownActionIds = requiredActions
            .Where(static action => action.LuminaActionId > 0)
            .Select(static action => action.LuminaActionId)
            .ToHashSet();

        IReadOnlyList<HotbarSlotEntry> hotbarSlots;
        try
        {
            hotbarSlots = hotbarDatReader.Read(files.HotbarBytes, crafterJob, knownActionIds);
        }
        catch (Exception exception) when (exception is UnsupportedCharacterConfigFormatException or InvalidDataException)
        {
            logger.LogWarning(exception, "Failed to parse HOTBAR.DAT.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "HOTBAR.DAT を解析できないため、シーケンスを準備できません。");
        }

        IReadOnlyList<KeybindEntry> keybindEntries;
        try
        {
            keybindEntries = keybindDatReader.Read(files.KeybindBytes);
        }
        catch (Exception exception) when (exception is UnsupportedCharacterConfigFormatException or InvalidDataException)
        {
            logger.LogWarning(exception, "Failed to parse KEYBIND.DAT.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "KEYBIND.DAT を解析できないため、シーケンスを準備できません。");
        }

        if (!keybindEntries.Any(static entry => entry.HasPrimaryOrSecondary))
        {
            return CraftSequenceExecutionPreparationResult.Failed(
                "ホットバーのキーバインドを取得できませんでした。KEYBIND.DAT のcommand解析ログを確認してください。");
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
                logger.LogInformation(
                    "Craft action execution binding check. Action: {ActionName}, LuminaActionId: {LuminaActionId}, HotbarSlot: {HotbarSlot}, Keybind: {Keybind}",
                    requiredAction.ActionName,
                    requiredAction.LuminaActionId,
                    "-",
                    "-");
                continue;
            }

            KeybindEntry? keybind = ResolveKeybindForHotbarSlot(keybindEntries, slot.HotbarId, slot.SlotId);
            if (keybind is null || !keybind.HasPrimaryOrSecondary)
            {
                unboundActions.Add(requiredAction);
                logger.LogInformation(
                    "Craft action execution binding check. Action: {ActionName}, LuminaActionId: {LuminaActionId}, HotbarSlot: {HotbarSlot}, Keybind: {Keybind}",
                    requiredAction.ActionName,
                    requiredAction.LuminaActionId,
                    $"Hotbar {slot.HotbarId} Slot {slot.SlotId}",
                    "-");
                continue;
            }

            KeybindGesture gesture = keybind.Primary ?? keybind.Secondary
                ?? throw new InvalidOperationException("KEYBIND.DAT のバインド解決結果が不正です。");

            logger.LogInformation(
                "Craft action execution binding check. Action: {ActionName}, LuminaActionId: {LuminaActionId}, HotbarSlot: {HotbarSlot}, Keybind: {Keybind}",
                requiredAction.ActionName,
                requiredAction.LuminaActionId,
                $"Hotbar {slot.HotbarId} Slot {slot.SlotId}",
                KeybindDisplayFormatter.Format(gesture));

            actionKeyBindings.Add(new CraftActionKeyBinding(
                requiredAction.ActionId,
                requiredAction.ActionName,
                slot.HotbarId,
                slot.SlotId,
                KeybindDisplayFormatter.Format(gesture),
                KeybindDisplayFormatter.ToKeys(gesture)));
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

    private static bool IsAvailableForCrafterJob(HotbarSlotEntry slot, CrafterJob crafterJob)
    {
        return slot.GroupId == 0 || slot.GroupId == crafterJob.ClassJobId;
    }

    private static KeybindEntry? ResolveKeybindForHotbarSlot(
        IReadOnlyList<KeybindEntry> keybindEntries,
        byte hotbarId,
        byte slotId)
    {
        foreach (KeybindEntry entry in keybindEntries)
        {
            if (!KeybindDatReader.TryResolveHotbarCommand(entry.Command, out int resolvedHotbarId, out int resolvedSlotId))
            {
                continue;
            }

            if (resolvedHotbarId == hotbarId && resolvedSlotId == slotId)
            {
                return entry;
            }
        }

        return null;
    }
}
