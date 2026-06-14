namespace XivLinker.Infrastructure.Lumina.Services;

public interface IGameDataService
{
    bool IsConfigured
    {
        get;
    }

    bool IsAvailable
    {
        get;
    }

    string? SqPackPath
    {
        get;
    }

    string? ErrorMessage
    {
        get;
    }

    Task<GameDataStatus> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
}
