using Lumina;

namespace XivLinker.Infrastructure.Lumina.Services;

public interface ILuminaGameDataProvider
{
    Task<GameData?> GetGameDataAsync(CancellationToken cancellationToken = default);

    Task<ResolvedMapLocation?> ResolveMapLocationAsync(
        uint? territoryTypeId,
        uint? mapId,
        string? mapName,
        float rawX,
        float rawY,
        float rawZ,
        CancellationToken cancellationToken = default);

    Task<ResolvedClassJobInfo?> ResolveClassJobAsync(
        uint classJobId,
        int? level = null,
        CancellationToken cancellationToken = default);
}
