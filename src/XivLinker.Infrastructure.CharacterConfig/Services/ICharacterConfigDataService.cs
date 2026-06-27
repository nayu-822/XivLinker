using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public interface ICharacterConfigDataService
{
    Task<CharacterData> LoadAsync(CharacterProfile profile, CancellationToken cancellationToken = default);
}
