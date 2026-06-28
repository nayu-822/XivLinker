using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Models;
using XivLinker.Domain.Models;
using XivLinker.Domain.Models.Crafting;
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
                "シーケンスに準備対象のクラフターアクションが含まれていないため、実行できません。");
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

        IReadOnlyList<HotbarSlotKeyBinding> keyBindings;
        try
        {
            keyBindings = keybindDatReader.ReadHotbarKeyBindings(files.KeybindBytes);
        }
        catch (Exception exception) when (exception is UnsupportedCharacterConfigFormatException or InvalidDataException)
        {
            logger.LogWarning(exception, "Failed to parse KEYBIND.DAT.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "KEYBIND.DAT を解析できないため、シーケンスを準備できません。");
        }

        if (keyBindings.Count == 0)
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
                slot.Kind == HotbarSlotKind.Action
                && slot.ActionOrCommandId == requiredAction.LuminaActionId
                && IsSlotAvailableForJob(slot, crafterJob));

            if (slot is null)
            {
                missingActions.Add(requiredAction);
                continue;
            }

            HotbarSlotKeyBinding? keyBinding = keyBindings.FirstOrDefault(binding =>
                binding.HotbarNumber == slot.HotbarNumber
                && binding.SlotNumber == slot.SlotNumber);

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

    private static bool IsSlotAvailableForJob(
        HotbarSlotEntry slot,
        CrafterJob crafterJob)
    {
        if (slot.IsShared)
        {
            return true;
        }

        return slot.ClassJobId is null
            || slot.ClassJobId == 0
            || slot.ClassJobId == crafterJob.ClassJobId;
    }
}
