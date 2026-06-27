namespace XivLinker.Infrastructure.Overlay.Models;

public sealed class CurrentPlayerState
{
    public bool IsConnected { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public uint? TerritoryTypeId { get; init; }

    public uint? MapId { get; init; }

    public float? RawX { get; init; }

    public float? RawY { get; init; }

    public float? RawZ { get; init; }

    public double? MapX { get; init; }

    public double? MapY { get; init; }

    public uint? ClassJobId { get; init; }

    public string ClassJobName { get; init; } = string.Empty;

    public int? Level { get; init; }

    public string MapName { get; init; } = string.Empty;

    public string CoordinatesText { get; init; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; init; }

    public string? IssueMessage { get; init; }
}
