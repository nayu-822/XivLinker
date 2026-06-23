using Lumina;

namespace XivLinker.Infrastructure.Lumina.Services;

public interface ILuminaGameDataProvider
{
    Task<GameData?> GetGameDataAsync(CancellationToken cancellationToken = default);
}
