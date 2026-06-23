namespace XivLinker.Domain.Models.Crafting;

public static class CraftActionCatalog
{
    private const string DefaultCategory = "クラフターアクション";

    private static readonly IReadOnlyList<CraftActionDefinition> Actions =
    [
        Create(CraftActionId.BasicSynthesis, "作業"),
        Create(CraftActionId.BasicTouch, "加工"),
        Create(CraftActionId.MastersMend, "マスターズメンド"),
        Create(CraftActionId.Veneration, "ヴェネレーション"),
        Create(CraftActionId.Innovation, "イノベーション"),
        Create(CraftActionId.GreatStrides, "グレートストライド"),
        Create(CraftActionId.ByregotsBlessing, "ビエルゴの祝福"),
    ];

    public static IReadOnlyList<CraftActionDefinition> GetAll()
    {
        return Actions;
    }

    public static CraftActionDefinition Get(CraftActionId actionId)
    {
        return Actions.First(definition => definition.ActionId == actionId);
    }

    public static bool TryGet(CraftActionId actionId, out CraftActionDefinition? definition)
    {
        definition = Actions.FirstOrDefault(definition => definition.ActionId == actionId);
        return definition is not null;
    }

    private static CraftActionDefinition Create(
        CraftActionId actionId,
        string displayName)
    {
        return new CraftActionDefinition(
            actionId,
            displayName,
            2500,
            DefaultCategory,
            0,
            []);
    }
}
