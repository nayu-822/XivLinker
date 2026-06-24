namespace XivLinker.Domain.Models.Crafting;

public readonly record struct CraftActionId(string Value)
{
    public static CraftActionId BasicSynthesis => new("craftaction:basic-synthesis");

    public static CraftActionId BasicTouch => new("craftaction:basic-touch");

    public static CraftActionId MastersMend => new("craftaction:masters-mend");

    public static CraftActionId StandardTouch => new("craftaction:standard-touch");

    public static CraftActionId Observe => new("craftaction:observe");

    public static CraftActionId PreciseTouch => new("craftaction:precise-touch");

    public static CraftActionId CarefulSynthesis => new("craftaction:careful-synthesis");

    public static CraftActionId PrudentTouch => new("craftaction:prudent-touch");

    public static CraftActionId TrainedEye => new("craftaction:trained-eye");

    public static CraftActionId PreparatoryTouch => new("craftaction:preparatory-touch");

    public static CraftActionId IntensiveSynthesis => new("craftaction:intensive-synthesis");

    public static CraftActionId DelicateSynthesis => new("craftaction:delicate-synthesis");

    public static CraftActionId Veneration => new("craftaction:veneration");

    public static CraftActionId Innovation => new("craftaction:innovation");

    public static CraftActionId GreatStrides => new("craftaction:great-strides");

    public static CraftActionId ByregotsBlessing => new("craftaction:byregots-blessing");

    public static CraftActionId HastyTouch => new("craftaction:hasty-touch");

    public static CraftActionId RapidSynthesis => new("craftaction:rapid-synthesis");

    public static CraftActionId TricksOfTheTrade => new("craftaction:tricks-of-the-trade");

    public static CraftActionId MuscleMemory => new("craftaction:muscle-memory");

    public static CraftActionId Reflect => new("craftaction:reflect");

    public static CraftActionId CarefulObservation => new("craftaction:careful-observation");

    public static CraftActionId Groundwork => new("craftaction:groundwork");

    public static CraftActionId AdvancedTouch => new("craftaction:advanced-touch");

    public static CraftActionId HeartAndSoul => new("craftaction:heart-and-soul");

    public static CraftActionId PrudentSynthesis => new("craftaction:prudent-synthesis");

    public static CraftActionId TrainedFinesse => new("craftaction:trained-finesse");

    public static CraftActionId RefinedTouch => new("craftaction:refined-touch");

    public static CraftActionId QuickInnovation => new("craftaction:quick-innovation");

    public static CraftActionId ImmaculateMend => new("craftaction:immaculate-mend");

    public static CraftActionId TrainedPerfection => new("craftaction:trained-perfection");

    public static CraftActionId WasteNot => new("craftaction:waste-not");

    public static CraftActionId WasteNotII => new("craftaction:waste-not-ii");

    public static CraftActionId Manipulation => new("craftaction:manipulation");

    public static CraftActionId FocusedSynthesis => new("craftaction:focused-synthesis");

    public static CraftActionId FocusedTouch => new("craftaction:focused-touch");

    public static CraftActionId FinalAppraisal => new("craftaction:final-appraisal");

    public static CraftActionId DaringTouch => new("craftaction:daring-touch");

    public override string ToString()
    {
        return Value;
    }
}
