namespace XivLinker.Domain.Models.Crafting;

public static class CraftActionCatalog
{
    private static readonly IReadOnlyList<CraftActionDefinition> Actions =
    [
        new(CraftActionId.BasicSynthesis, "作業", 2500, "作業"),
        new(CraftActionId.BasicTouch, "加工", 2500, "品質"),
        new(CraftActionId.MastersMend, "マスターズメンド", 2500, "回復"),
        new(CraftActionId.Veneration, "ヴェネレーション", 2500, "バフ"),
        new(CraftActionId.Innovation, "イノベーション", 2500, "バフ"),
        new(CraftActionId.GreatStrides, "グレートストライド", 2500, "バフ"),
        new(CraftActionId.ByregotsBlessing, "ビエルゴの祝福", 2500, "品質"),
    ];

    public static IReadOnlyList<CraftActionDefinition> GetAll()
    {
        return Actions;
    }

    public static CraftActionDefinition Get(CraftActionId actionId)
    {
        return Actions.First(definition => definition.ActionId == actionId);
    }
}
