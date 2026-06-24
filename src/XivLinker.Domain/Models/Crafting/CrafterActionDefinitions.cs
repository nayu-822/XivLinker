namespace XivLinker.Domain.Models.Crafting;

public static class CrafterActionDefinitions
{
    private const string DefaultCategory = "クラフターアクション";
    private const int StandardActionWaitMilliseconds = 3000;
    private const int StatusActionWaitMilliseconds = 2000;

    private static readonly IReadOnlyList<CrafterClassJobInfo> CrafterJobs =
    [
        new(8u, "CRP", "木工師"),
        new(9u, "BSM", "鍛冶師"),
        new(10u, "ARM", "甲冑師"),
        new(11u, "GSM", "彫金師"),
        new(12u, "LTW", "革細工師"),
        new(13u, "WVR", "裁縫師"),
        new(14u, "ALC", "錬金術師"),
        new(15u, "CUL", "調理師"),
    ];

    public static IReadOnlyList<CraftActionDefinition> All { get; } =
    [
        Create(CraftActionId.BasicSynthesis, "作業", [100001u, 100015u, 100030u, 100075u, 100045u, 100060u, 100090u, 100105u], [1501u, 1551u, 1601u, 1651u, 1701u, 1751u, 1801u, 1851u]),
        Create(CraftActionId.BasicTouch, "加工", [100002u, 100016u, 100031u, 100076u, 100046u, 100061u, 100091u, 100106u], [1502u, 1552u, 1602u, 1652u, 1702u, 1752u, 1802u, 1852u]),
        Create(CraftActionId.MastersMend, "マスターズメンド", [100003u, 100017u, 100032u, 100077u, 100047u, 100062u, 100092u, 100107u], [1952u, 1952u, 1952u, 1952u, 1952u, 1952u, 1952u, 1952u]),
        Create(CraftActionId.StandardTouch, "中級加工", [100004u, 100018u, 100034u, 100078u, 100048u, 100064u, 100093u, 100109u], [1516u, 1566u, 1616u, 1665u, 1716u, 1765u, 1816u, 1865u]),
        Create(CraftActionId.Observe, "経過観察", [100010u, 100023u, 100040u, 100082u, 100053u, 100070u, 100099u, 100113u], [1954u, 1954u, 1954u, 1954u, 1954u, 1954u, 1954u, 1954u], StatusActionWaitMilliseconds),
        Create(CraftActionId.PreciseTouch, "集中加工", [100128u, 100129u, 100130u, 100131u, 100132u, 100133u, 100134u, 100135u], [1524u, 1574u, 1625u, 1676u, 1724u, 1774u, 1825u, 1875u]),
        Create(CraftActionId.CarefulSynthesis, "模範作業", [100203u, 100204u, 100205u, 100206u, 100207u, 100208u, 100209u, 100210u], [1986u, 1986u, 1986u, 1986u, 1986u, 1986u, 1986u, 1986u]),
        Create(CraftActionId.PrudentTouch, "倹約加工", [100227u, 100228u, 100229u, 100230u, 100231u, 100232u, 100233u, 100234u], [1535u, 1584u, 1635u, 1686u, 1734u, 1784u, 1835u, 1886u]),
        Create(CraftActionId.TrainedEye, "匠の早業", [100283u, 100284u, 100285u, 100286u, 100287u, 100288u, 100289u, 100290u], [1981u, 1981u, 1981u, 1981u, 1981u, 1981u, 1981u, 1981u]),
        Create(CraftActionId.PreparatoryTouch, "下地加工", [100299u, 100300u, 100301u, 100302u, 100303u, 100304u, 100305u, 100306u], [1507u, 1557u, 1607u, 1657u, 1707u, 1757u, 1807u, 1857u]),
        Create(CraftActionId.IntensiveSynthesis, "集中作業", [100315u, 100316u, 100317u, 100318u, 100319u, 100320u, 100321u, 100322u], [1514u, 1564u, 1614u, 1663u, 1714u, 1763u, 1814u, 1863u]),
        Create(CraftActionId.DelicateSynthesis, "精密作業", [100323u, 100324u, 100325u, 100326u, 100327u, 100328u, 100329u, 100330u], [1503u, 1553u, 1603u, 1653u, 1703u, 1753u, 1803u, 1853u]),
        Create(CraftActionId.ByregotsBlessing, "ビエルゴの祝福", [100339u, 100340u, 100341u, 100342u, 100343u, 100344u, 100345u, 100346u], [1975u, 1975u, 1975u, 1975u, 1975u, 1975u, 1975u, 1975u]),
        Create(CraftActionId.HastyTouch, "ヘイスティタッチ", [100355u, 100356u, 100357u, 100358u, 100359u, 100360u, 100361u, 100362u], [1989u, 1989u, 1989u, 1989u, 1989u, 1989u, 1989u, 1989u]),
        Create(CraftActionId.RapidSynthesis, "突貫作業", [100363u, 100364u, 100365u, 100366u, 100367u, 100368u, 100369u, 100370u], [1988u, 1988u, 1988u, 1988u, 1988u, 1988u, 1988u, 1988u]),
        Create(CraftActionId.TricksOfTheTrade, "秘訣", [100371u, 100372u, 100373u, 100374u, 100375u, 100376u, 100377u, 100378u], [1990u, 1990u, 1990u, 1990u, 1990u, 1990u, 1990u, 1990u], StatusActionWaitMilliseconds),
        Create(CraftActionId.MuscleMemory, "確信", [100379u, 100380u, 100381u, 100382u, 100383u, 100384u, 100385u, 100386u], [1994u, 1994u, 1994u, 1994u, 1994u, 1994u, 1994u, 1994u]),
        Create(CraftActionId.Reflect, "真価", [100387u, 100388u, 100389u, 100390u, 100391u, 100392u, 100393u, 100394u], [1982u, 1982u, 1982u, 1982u, 1982u, 1982u, 1982u, 1982u]),
        Create(CraftActionId.CarefulObservation, "設計変更", [100395u, 100396u, 100397u, 100398u, 100399u, 100400u, 100401u, 100402u], [1984u, 1984u, 1984u, 1984u, 1984u, 1984u, 1984u, 1984u], StatusActionWaitMilliseconds),
        Create(CraftActionId.Groundwork, "下地作業", [100403u, 100404u, 100405u, 100406u, 100407u, 100408u, 100409u, 100410u], [1518u, 1568u, 1618u, 1667u, 1718u, 1767u, 1818u, 1867u]),
        Create(CraftActionId.AdvancedTouch, "上級加工", [100411u, 100412u, 100413u, 100414u, 100415u, 100416u, 100417u, 100418u], [1519u, 1569u, 1620u, 1669u, 1719u, 1769u, 1820u, 1869u]),
        Create(CraftActionId.HeartAndSoul, "一心不乱", [100419u, 100420u, 100421u, 100422u, 100423u, 100424u, 100425u, 100426u], [1996u, 1996u, 1996u, 1996u, 1996u, 1996u, 1996u, 1996u], StatusActionWaitMilliseconds),
        Create(CraftActionId.PrudentSynthesis, "倹約作業", [100427u, 100428u, 100429u, 100430u, 100431u, 100432u, 100433u, 100434u], [1520u, 1570u, 1621u, 1670u, 1720u, 1770u, 1821u, 1870u]),
        Create(CraftActionId.TrainedFinesse, "匠の神業", [100435u, 100436u, 100437u, 100438u, 100439u, 100440u, 100441u, 100442u], [1997u, 1997u, 1997u, 1997u, 1997u, 1997u, 1997u, 1997u]),
        Create(CraftActionId.RefinedTouch, "洗練加工", [100443u, 100444u, 100445u, 100446u, 100447u, 100448u, 100449u, 100450u], [1522u, 1572u, 1623u, 1674u, 1722u, 1772u, 1823u, 1873u]),
        Create(CraftActionId.QuickInnovation, "クイックイノベーション", [100459u, 100460u, 100461u, 100462u, 100463u, 100464u, 100465u, 100466u], [1999u, 1999u, 1999u, 1999u, 1999u, 1999u, 1999u, 1999u], StatusActionWaitMilliseconds),
        Create(CraftActionId.ImmaculateMend, "パーフェクトメンド", [100467u, 100468u, 100469u, 100470u, 100471u, 100472u, 100473u, 100474u], [1950u, 1950u, 1950u, 1950u, 1950u, 1950u, 1950u, 1950u]),
        Create(CraftActionId.TrainedPerfection, "匠の絶技", [100475u, 100476u, 100477u, 100478u, 100479u, 100480u, 100481u, 100482u], [1926u, 1926u, 1926u, 1926u, 1926u, 1926u, 1926u, 1926u], StatusActionWaitMilliseconds),
        Create(CraftActionId.WasteNot, "倹約", [4631u, 4632u, 4633u, 4634u, 4635u, 4636u, 4637u, 4638u], [1992u, 1992u, 1992u, 1992u, 1992u, 1992u, 1992u, 1992u], StatusActionWaitMilliseconds),
        Create(CraftActionId.Veneration, "ヴェネレーション", [19297u, 19298u, 19299u, 19300u, 19301u, 19302u, 19303u, 19304u], [1995u, 1995u, 1995u, 1995u, 1995u, 1995u, 1995u, 1995u], StatusActionWaitMilliseconds),
        Create(CraftActionId.GreatStrides, "グレートストライド", [260u, 261u, 262u, 263u, 264u, 265u, 266u, 267u], [1955u, 1955u, 1955u, 1955u, 1955u, 1955u, 1955u, 1955u], StatusActionWaitMilliseconds),
        Create(CraftActionId.Innovation, "イノベーション", [19004u, 19005u, 19006u, 19007u, 19008u, 19009u, 19010u, 19011u], [1987u, 1987u, 1987u, 1987u, 1987u, 1987u, 1987u, 1987u], StatusActionWaitMilliseconds),
        Create(CraftActionId.WasteNotII, "長期倹約", [4639u, 4640u, 4641u, 4642u, 4643u, 4644u, 19002u, 19003u], [1993u, 1993u, 1993u, 1993u, 1993u, 1993u, 1993u, 1993u], StatusActionWaitMilliseconds),
        Create(CraftActionId.Manipulation, "マニピュレーション", [4574u, 4575u, 4576u, 4577u, 4578u, 4579u, 4580u, 4581u], [1985u, 1985u, 1985u, 1985u, 1985u, 1985u, 1985u, 1985u], StatusActionWaitMilliseconds),
        Create(CraftActionId.FocusedSynthesis, "注視作業", [100235u, 100236u, 100237u, 100238u, 100239u, 100240u, 100241u, 100242u], [786u, 786u, 786u, 786u, 786u, 786u, 786u, 786u]),
        Create(CraftActionId.FocusedTouch, "注視加工", [100243u, 100244u, 100245u, 100246u, 100247u, 100248u, 100249u, 100250u], [786u, 786u, 786u, 786u, 786u, 786u, 786u, 786u]),
        Create(CraftActionId.FinalAppraisal, "最終確認", [19012u, 19013u, 19014u, 19015u, 19016u, 19017u, 19018u, 19019u], [1983u, 1983u, 1983u, 1983u, 1983u, 1983u, 1983u, 1983u], StatusActionWaitMilliseconds),
        Create(CraftActionId.DaringTouch, "デアリングタッチ", [100451u, 100452u, 100453u, 100454u, 100455u, 100456u, 100457u, 100458u], [1998u, 1998u, 1998u, 1998u, 1998u, 1998u, 1998u, 1998u]),
    ];

    public static IReadOnlyList<CrafterActionPaletteCategoryDefinition> PaletteCategories { get; } =
    [
        new(
            "作業系",
            [
                CraftActionId.BasicSynthesis,
                CraftActionId.CarefulSynthesis,
                CraftActionId.IntensiveSynthesis,
                CraftActionId.DelicateSynthesis,
                CraftActionId.RapidSynthesis,
                CraftActionId.MuscleMemory,
                CraftActionId.Groundwork,
                CraftActionId.PrudentSynthesis,
            ]),
        new(
            "加工系",
            [
                CraftActionId.BasicTouch,
                CraftActionId.StandardTouch,
                CraftActionId.PreciseTouch,
                CraftActionId.PrudentTouch,
                CraftActionId.PreparatoryTouch,
                CraftActionId.ByregotsBlessing,
                CraftActionId.HastyTouch,
                CraftActionId.AdvancedTouch,
                CraftActionId.TrainedFinesse,
                CraftActionId.RefinedTouch,
                CraftActionId.DaringTouch,
                CraftActionId.Reflect,
            ]),
        new(
            "バフ・補助系",
            [
                CraftActionId.MastersMend,
                CraftActionId.ImmaculateMend,
                CraftActionId.Manipulation,
                CraftActionId.Veneration,
                CraftActionId.Observe,
                CraftActionId.TrainedEye,
                CraftActionId.TricksOfTheTrade,
                CraftActionId.WasteNot,
                CraftActionId.WasteNotII,
                CraftActionId.GreatStrides,
                CraftActionId.Innovation,
                CraftActionId.FinalAppraisal,
                CraftActionId.TrainedPerfection,
            ]),
        new(
            "専門技能系",
            [
                CraftActionId.CarefulObservation,
                CraftActionId.HeartAndSoul,
                CraftActionId.QuickInnovation,
            ]),
    ];

    private static readonly IReadOnlyDictionary<CraftActionId, CraftActionDefinition> DefinitionsByActionId =
        All.ToDictionary(static definition => definition.ActionId);

    private static readonly IReadOnlyDictionary<uint, CraftActionDefinition> DefinitionsByLuminaRowId =
        All.SelectMany(static definition => definition.Variants.Select(variant => new KeyValuePair<uint, CraftActionDefinition>(variant.LuminaRowId, definition)))
            .ToDictionary();

    public static CraftActionDefinition Get(CraftActionId actionId)
    {
        return DefinitionsByActionId[actionId];
    }

    public static bool TryGet(CraftActionId actionId, out CraftActionDefinition? definition)
    {
        return DefinitionsByActionId.TryGetValue(actionId, out definition);
    }

    public static bool TryGetByLuminaRowId(uint luminaRowId, out CraftActionDefinition? definition)
    {
        return DefinitionsByLuminaRowId.TryGetValue(luminaRowId, out definition);
    }

    private static CraftActionDefinition Create(
        CraftActionId actionId,
        string displayName,
        IReadOnlyList<uint> luminaRowIds,
        IReadOnlyList<uint> iconIds,
        int postActionWaitMilliseconds = StandardActionWaitMilliseconds)
    {
        if (luminaRowIds.Count != CrafterJobs.Count)
        {
            throw new ArgumentException("Lumina row ID count does not match crafter job count.", nameof(luminaRowIds));
        }

        if (iconIds.Count != CrafterJobs.Count)
        {
            throw new ArgumentException("Icon ID count does not match crafter job count.", nameof(iconIds));
        }

        CrafterActionVariant[] variants = new CrafterActionVariant[CrafterJobs.Count];

        for (int index = 0; index < CrafterJobs.Count; index++)
        {
            CrafterClassJobInfo crafterJob = CrafterJobs[index];
            variants[index] = new CrafterActionVariant(
                luminaRowIds[index],
                iconIds[index],
                crafterJob.ClassJobRowId,
                crafterJob.ClassJobAbbreviation,
                crafterJob.ClassJobName);
        }

        return new CraftActionDefinition(
            actionId,
            displayName,
            postActionWaitMilliseconds,
            DefaultCategory,
            variants[0].IconId,
            variants);
    }

    public sealed record CrafterActionPaletteCategoryDefinition(
        string CategoryName,
        IReadOnlyList<CraftActionId> ActionIds);

    private sealed record CrafterClassJobInfo(
        uint ClassJobRowId,
        string ClassJobAbbreviation,
        string ClassJobName);
}
