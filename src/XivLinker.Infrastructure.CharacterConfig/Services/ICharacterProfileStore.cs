using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public interface ICharacterProfileStore
{
    event EventHandler? StateChanged;

    IReadOnlyList<CharacterProfile> Profiles { get; }

    CharacterProfile? SelectedProfile { get; }

    CharacterData? SelectedCharacterData { get; }

    Task AddProfileAsync(string path, CancellationToken cancellationToken = default);

    Task SelectProfileAsync(string profileId, CancellationToken cancellationToken = default);

    Task ReloadProfileAsync(string profileId, CancellationToken cancellationToken = default);

    Task RemoveProfileAsync(string profileId, CancellationToken cancellationToken = default);
}
