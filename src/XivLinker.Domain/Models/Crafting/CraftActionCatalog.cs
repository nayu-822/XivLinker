namespace XivLinker.Domain.Models.Crafting;

public static class CraftActionCatalog
{
    public const int DefaultPostActionWaitMilliseconds = 3000;

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

    public static int ResolvePostActionWaitMilliseconds(CraftActionId actionId)
    {
        return TryGet(actionId, out CraftActionDefinition? definition) && definition is not null
            ? definition.PostActionWaitMilliseconds
            : DefaultPostActionWaitMilliseconds;
    }
}
