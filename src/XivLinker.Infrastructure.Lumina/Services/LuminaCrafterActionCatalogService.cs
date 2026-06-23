using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaCrafterActionCatalogService : ICrafterActionCatalogService
{
    private const string DefaultCategory = "クラフターアクション";

    private static readonly IReadOnlyDictionary<string, CraftActionId> KnownIds = new Dictionary<string, CraftActionId>(StringComparer.OrdinalIgnoreCase)
    {
        ["basic synthesis"] = CraftActionId.BasicSynthesis,
        ["basic touch"] = CraftActionId.BasicTouch,
        ["master's mend"] = CraftActionId.MastersMend,
        ["veneration"] = CraftActionId.Veneration,
        ["innovation"] = CraftActionId.Innovation,
        ["great strides"] = CraftActionId.GreatStrides,
        ["byregot's blessing"] = CraftActionId.ByregotsBlessing,
    };

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
        var englishSheet = gameData.GetExcelSheet<CraftAction>(Language.English);
        if (japaneseSheet is null)
        {
            return CraftActionCatalog.GetAll();
        }

        Dictionary<uint, CraftAction> japaneseRows = japaneseSheet
            .Where(static row => !string.IsNullOrWhiteSpace(row.Name.ToString().Trim()))
            .ToDictionary(static row => row.RowId);
        Dictionary<uint, CraftAction> englishRows = englishSheet?
            .Where(static row => !string.IsNullOrWhiteSpace(row.Name.ToString().Trim()))
            .ToDictionary(static row => row.RowId)
            ?? [];

        if (japaneseRows.Count == 0)
        {
            return CraftActionCatalog.GetAll();
        }

        var graph = japaneseRows.Keys.ToDictionary(static rowId => rowId, static _ => new HashSet<uint>());

        foreach (CraftAction row in japaneseRows.Values)
        {
            foreach (uint relatedRowId in EnumerateRelatedRowIds(row))
            {
                if (!japaneseRows.ContainsKey(relatedRowId))
                {
                    continue;
                }

                graph[row.RowId].Add(relatedRowId);
                graph[relatedRowId].Add(row.RowId);
            }
        }

        var visited = new HashSet<uint>();
        var definitions = new List<CraftActionDefinition>();

        foreach (uint startRowId in graph.Keys.Order())
        {
            if (!visited.Add(startRowId))
            {
                continue;
            }

            List<uint> component = Traverse(graph, startRowId, visited);
            List<CraftAction> componentRows = component
                .Select(rowId => japaneseRows[rowId])
                .OrderBy(static row => row.RowId)
                .ToList();
            CraftAction representativeRow = componentRows[0];
            CraftAction? representativeEnglishRow = englishRows.GetValueOrDefault(representativeRow.RowId);

            string displayName = representativeRow.Name.ToString().Trim();
            string englishName = representativeEnglishRow?.Name.ToString().Trim() ?? displayName;
            CraftActionId actionId = ResolveActionId(englishName);

            CrafterActionVariant[] variants = componentRows
                .Select(row => CreateVariant(row, englishRows.GetValueOrDefault(row.RowId)))
                .OrderBy(static variant => variant.ClassJobAbbreviation, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static variant => variant.LuminaRowId)
                .ToArray();

            CrafterActionVariant? representativeVariant = variants.FirstOrDefault(static variant => variant.IconId != 0);
            uint representativeIconId = representativeVariant?.IconId ?? 0;

            CraftActionDefinition definition = CraftActionCatalog.TryGet(actionId, out CraftActionDefinition? fallback)
                && fallback is not null
                ? fallback with
                {
                    RepresentativeIconId = representativeIconId,
                    Variants = variants,
                }
                : new CraftActionDefinition(
                    actionId,
                    displayName,
                    2500,
                    DefaultCategory,
                    representativeIconId,
                    variants);

            definitions.Add(definition);
        }

        return definitions
            .DistinctBy(static definition => definition.ActionId)
            .OrderBy(static definition => definition.Category, StringComparer.CurrentCulture)
            .ThenBy(static definition => definition.DisplayName, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static List<uint> Traverse(IReadOnlyDictionary<uint, HashSet<uint>> graph, uint startRowId, ISet<uint> visited)
    {
        var queue = new Queue<uint>();
        var component = new List<uint>();
        queue.Enqueue(startRowId);

        while (queue.Count > 0)
        {
            uint current = queue.Dequeue();
            component.Add(current);

            foreach (uint next in graph[current])
            {
                if (!visited.Add(next))
                {
                    continue;
                }

                queue.Enqueue(next);
            }
        }

        return component;
    }

    private static IEnumerable<uint> EnumerateRelatedRowIds(CraftAction row)
    {
        yield return row.RowId;

        foreach (var reference in new[]
        {
            row.ALC,
            row.ARM,
            row.BSM,
            row.CRP,
            row.CUL,
            row.GSM,
            row.LTW,
            row.WVR,
        })
        {
            if (reference.IsValid && reference.RowId != 0)
            {
                yield return reference.RowId;
            }
        }
    }

    private static CrafterActionVariant CreateVariant(CraftAction row, CraftAction? englishRow)
    {
        ClassJob? classJob = row.ClassJob.ValueNullable;
        string? classJobName = classJob?.Name.ToString().Trim();
        string? classJobAbbreviation = classJob?.Abbreviation.ToString().Trim();

        return new CrafterActionVariant(
            row.RowId,
            row.Icon,
            row.ClassJob.IsValid ? row.ClassJob.RowId : null,
            string.IsNullOrWhiteSpace(classJobAbbreviation) ? null : classJobAbbreviation,
            string.IsNullOrWhiteSpace(classJobName) ? englishRow?.Name.ToString().Trim() : classJobName);
    }

    private static CraftActionId ResolveActionId(string englishName)
    {
        if (KnownIds.TryGetValue(englishName.Trim(), out CraftActionId actionId))
        {
            return actionId;
        }

        return new CraftActionId($"craftaction:{ToSlug(englishName)}");
    }

    private static string ToSlug(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int count = 0;
        bool previousDash = false;

        foreach (char character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[count++] = character;
                previousDash = false;
                continue;
            }

            if (previousDash || count == 0)
            {
                continue;
            }

            buffer[count++] = '-';
            previousDash = true;
        }

        string slug = new string(buffer[..count]).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "unknown" : slug;
    }
}
