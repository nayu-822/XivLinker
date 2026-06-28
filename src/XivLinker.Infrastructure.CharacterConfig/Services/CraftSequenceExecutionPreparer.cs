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

        try
        {
            _ = hotbarDatReader.Read(files.HotbarBytes, crafterJob);
            _ = keybindDatReader.Read(files.KeybindBytes);
        }
        catch (UnsupportedCharacterConfigFormatException exception)
        {
            logger.LogWarning(exception, "Unsupported character config format.");
            return CraftSequenceExecutionPreparationResult.Failed(exception.Message);
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(exception, "Failed to parse HOTBAR.DAT / KEYBIND.DAT for craft execution preparation.");
            return CraftSequenceExecutionPreparationResult.Failed(
                "HOTBAR.DAT または KEYBIND.DAT を解析できないため、シーケンスを準備できません。");
        }

        return CraftSequenceExecutionPreparationResult.Failed(
            "HOTBAR.DAT または KEYBIND.DAT の実ファイル形式にまだ対応できていないため、シーケンスを準備できません。");
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
