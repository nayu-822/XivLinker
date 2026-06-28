namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class ResolvedMapLocation
{
    public uint MapId { get; init; }

    public string MapName { get; init; } = string.Empty;

    public string? ResolutionSource { get; init; }

    public bool TerritoryTypeFound { get; init; }

    public bool TerritoryMapFound { get; init; }

    public short OffsetX { get; init; }

    public short OffsetY { get; init; }

    public ushort SizeFactor { get; init; }

    public double MapX { get; init; }

    public double MapY { get; init; }

    public string CoordinatesText { get; init; } = "未取得";

    public string? IssueMessage { get; init; }
}
