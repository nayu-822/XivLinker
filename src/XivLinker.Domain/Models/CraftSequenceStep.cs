using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Domain.Models;

public sealed class CraftSequenceStep
{
    public CraftActionId ActionId
    {
        get; set;
    }

    public int WaitMilliseconds
    {
        get; set;
    }
}
