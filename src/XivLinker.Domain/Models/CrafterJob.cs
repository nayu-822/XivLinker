namespace XivLinker.Domain.Models;

public sealed record CrafterJob(
    uint ClassJobId,
    string Name,
    string ShortName);

public static class CrafterJobs
{
    public static readonly CrafterJob Carpenter = new(8, "木工師", "CRP");
    public static readonly CrafterJob Blacksmith = new(9, "鍛冶師", "BSM");
    public static readonly CrafterJob Armorer = new(10, "甲冑師", "ARM");
    public static readonly CrafterJob Goldsmith = new(11, "彫金師", "GSM");
    public static readonly CrafterJob Leatherworker = new(12, "革細工師", "LTW");
    public static readonly CrafterJob Weaver = new(13, "裁縫師", "WVR");
    public static readonly CrafterJob Alchemist = new(14, "錬金術師", "ALC");
    public static readonly CrafterJob Culinarian = new(15, "調理師", "CUL");

    public static IReadOnlyList<CrafterJob> All { get; } =
    [
        Carpenter,
        Blacksmith,
        Armorer,
        Goldsmith,
        Leatherworker,
        Weaver,
        Alchemist,
        Culinarian,
    ];

    public static CrafterJob? FindByClassJobId(uint classJobId)
    {
        return All.FirstOrDefault(job => job.ClassJobId == classJobId);
    }
}
