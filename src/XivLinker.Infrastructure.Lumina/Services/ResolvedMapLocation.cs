namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class ResolvedMapLocation
{
    public uint MapId { get; init; }

    public string MapName { get; init; } = string.Empty;

    public double MapX { get; init; }

    public double MapY { get; init; }

    public string CoordinatesText { get; init; } = "未取得";

    public string? IssueMessage { get; init; }
}
