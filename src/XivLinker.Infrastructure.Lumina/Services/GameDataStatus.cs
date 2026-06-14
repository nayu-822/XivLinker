namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class GameDataStatus
{
    public required GameDataAvailabilityState State { get; init; }

    public required bool IsConfigured { get; init; }

    public required bool IsAvailable { get; init; }

    public string? SqPackPath { get; init; }

    public string? ErrorMessage { get; init; }
}
