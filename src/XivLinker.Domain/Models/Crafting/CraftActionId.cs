namespace XivLinker.Domain.Models.Crafting;

public readonly record struct CraftActionId(string Value)
{
    public static CraftActionId BasicSynthesis => new("craftaction:basic-synthesis");

    public static CraftActionId BasicTouch => new("craftaction:basic-touch");

    public static CraftActionId MastersMend => new("craftaction:masters-mend");

    public static CraftActionId Veneration => new("craftaction:veneration");

    public static CraftActionId Innovation => new("craftaction:innovation");

    public static CraftActionId GreatStrides => new("craftaction:great-strides");

    public static CraftActionId ByregotsBlessing => new("craftaction:byregots-blessing");

    public static CraftActionId FromLuminaRowId(uint rowId)
    {
        return new($"lumina-craftaction:{rowId}");
    }

    public override string ToString()
    {
        return Value;
    }
}
