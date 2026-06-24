using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaCrafterActionCatalogService : ICrafterActionCatalogService
{
    private readonly IGameDataService gameDataService;
    private readonly ILuminaGameDataProvider gameDataProvider;
    private readonly LuminaActionIconService iconService;
    private readonly SemaphoreSlim catalogLock = new(1, 1);
    private IReadOnlyList<CraftActionDefinition>? cachedActions;

    public LuminaCrafterActionCatalogService(
        IGameDataService gameDataService,
        ILuminaGameDataProvider gameDataProvider,
        LuminaActionIconService iconService)
    {
        this.gameDataService = gameDataService;
        this.gameDataProvider = gameDataProvider;
        this.iconService = iconService;
    }

    public async Task<CrafterActionCatalogResult> GetCrafterActionsAsync(CancellationToken cancellationToken = default)
    {
        GameDataStatus status = await gameDataService.CheckAvailabilityAsync(cancellationToken);
        if (!status.IsAvailable)
        {
            return new CrafterActionCatalogResult(
                CraftActionCatalog.GetAll(),
                status.ErrorMessage ?? "Lumina のクラフターアクション一覧を読み込めませんでした。");
        }

        if (cachedActions is not null)
        {
            return new CrafterActionCatalogResult(cachedActions);
        }

        await catalogLock.WaitAsync(cancellationToken);

        try
        {
            if (cachedActions is not null)
            {
                return new CrafterActionCatalogResult(cachedActions);
            }

            GameData? gameData = await gameDataProvider.GetGameDataAsync(cancellationToken);
            if (gameData is null)
            {
                return new CrafterActionCatalogResult(
                    CraftActionCatalog.GetAll(),
                    "Lumina のゲームデータを利用できませんでした。");
            }

            cachedActions = await Task.Run(() => BuildCommonActions(gameData), cancellationToken);
            return new CrafterActionCatalogResult(cachedActions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CrafterActionCatalogResult(CraftActionCatalog.GetAll(), exception.Message);
        }
        finally
        {
            catalogLock.Release();
        }
    }

    public Task<byte[]?> GetIconPngAsync(uint iconId, CancellationToken cancellationToken = default)
    {
        return iconService.GetIconPngAsync(iconId, cancellationToken);
    }

    private static IReadOnlyList<CraftActionDefinition> BuildCommonActions(GameData gameData)
    {
        var japaneseSheet = gameData.GetExcelSheet<CraftAction>(Language.Japanese);
        if (japaneseSheet is null)
        {
            return CrafterActionDefinitions.All;
        }

        var definitions = new List<CraftActionDefinition>();
        Dictionary<uint, CraftAction> rowsByRowId = japaneseSheet.ToDictionary(static row => row.RowId);

        foreach (CraftActionDefinition definition in CrafterActionDefinitions.All)
        {
            definitions.Add(ApplyLuminaData(definition, rowsByRowId));
        }

        return definitions;
    }

    private static CraftActionDefinition ApplyLuminaData(
        CraftActionDefinition definition,
        IReadOnlyDictionary<uint, CraftAction> rowsByRowId)
    {
        string displayName = definition.DisplayName;
        uint representativeIconId = definition.RepresentativeIconId;
        var variants = new CrafterActionVariant[definition.Variants.Count];

        for (int index = 0; index < definition.Variants.Count; index++)
        {
            CrafterActionVariant variant = definition.Variants[index];

            if (!rowsByRowId.ContainsKey(variant.LuminaRowId))
            {
                variants[index] = variant;
                continue;
            }

            CraftAction row = rowsByRowId[variant.LuminaRowId];

            string luminaName = row.Name.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(luminaName) && displayName == definition.DisplayName)
            {
                displayName = luminaName;
            }

            uint iconId = row.Icon != 0 ? row.Icon : variant.IconId;
            if (representativeIconId == definition.RepresentativeIconId && iconId != 0)
            {
                representativeIconId = iconId;
            }

            ClassJob? classJob = row.ClassJob.ValueNullable;
            string? classJobName = classJob?.Name.ToString().Trim();
            string? classJobAbbreviation = classJob?.Abbreviation.ToString().Trim();

            variants[index] = variant with
            {
                IconId = iconId,
                ClassJobRowId = row.ClassJob.IsValid ? row.ClassJob.RowId : variant.ClassJobRowId,
                ClassJobAbbreviation = string.IsNullOrWhiteSpace(classJobAbbreviation) ? variant.ClassJobAbbreviation : classJobAbbreviation,
                ClassJobName = string.IsNullOrWhiteSpace(classJobName) ? variant.ClassJobName : classJobName,
            };
        }

        return definition with
        {
            DisplayName = displayName,
            RepresentativeIconId = representativeIconId,
            Variants = variants,
        };
    }
}
