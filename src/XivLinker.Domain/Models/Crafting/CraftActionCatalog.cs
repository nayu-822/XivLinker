namespace XivLinker.Domain.Models.Crafting;

public static class CraftActionCatalog
{
    public static IReadOnlyList<CraftActionDefinition> GetAll()
    {
        return CrafterActionDefinitions.All;
    }

    public static CraftActionDefinition Get(CraftActionId actionId)
    {
        return CrafterActionDefinitions.Get(actionId);
    }

    public static bool TryGet(CraftActionId actionId, out CraftActionDefinition? definition)
    {
        return CrafterActionDefinitions.TryGet(actionId, out definition);
    }
}
