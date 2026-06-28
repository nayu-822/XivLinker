namespace XivLinker.Infrastructure.Overlay.Models;

public sealed class OverlayCurrentPlayerSnapshot
{
    public string PlayerName { get; init; } = string.Empty;

    public string RawCombatantJson { get; init; } = string.Empty;

    public uint? TerritoryTypeId { get; init; }

    public uint? MapId { get; init; }

    public float RawX { get; init; }

    public float RawY { get; init; }

    public float RawZ { get; init; }

    public uint? ClassJobId { get; init; }

    public int? Level { get; init; }
}
