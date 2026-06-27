using Microsoft.Extensions.Logging;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CharacterProfileStore : ICharacterProfileStore
{
    private readonly ICharacterConfigDataService characterConfigDataService;
    private readonly ILogger<CharacterProfileStore> logger;
    private readonly List<CharacterProfile> profiles = [];

    public CharacterProfileStore(
        ICharacterConfigDataService characterConfigDataService,
        ILogger<CharacterProfileStore> logger)
    {
        this.characterConfigDataService = characterConfigDataService;
        this.logger = logger;
    }

    public event EventHandler? StateChanged;

    public IReadOnlyList<CharacterProfile> Profiles => profiles;

    public CharacterProfile? SelectedProfile { get; private set; }

    public CharacterData? SelectedCharacterData { get; private set; }

    public async Task AddProfileAsync(string path, CancellationToken cancellationToken = default)
    {
        string normalizedPath = Path.GetFullPath(path);
        CharacterProfile? existing = profiles.FirstOrDefault(profile =>
            profile.CharacterSettingsDirectory.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            await SelectProfileAsync(existing.Id, cancellationToken);
            return;
        }

        var profile = new CharacterProfile
        {
            DisplayName = Path.GetFileName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            CharacterSettingsDirectory = normalizedPath,
        };

        profiles.Add(profile);
        RaiseStateChanged();
        await SelectProfileAsync(profile.Id, cancellationToken);
    }

    public async Task SelectProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        CharacterProfile? selected = profiles.FirstOrDefault(profile => profile.Id == profileId);
        if (selected is null)
        {
            return;
        }

        foreach (CharacterProfile profile in profiles)
        {
            profile.IsSelected = profile.Id == profileId;
        }

        SelectedProfile = selected;
        SelectedCharacterData = null;
        RaiseStateChanged();
        await LoadSelectedProfileAsync(selected, cancellationToken);
    }

    public async Task ReloadProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        CharacterProfile? profile = profiles.FirstOrDefault(candidate => candidate.Id == profileId);
        if (profile is null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedProfile, profile))
        {
            foreach (CharacterProfile candidate in profiles)
            {
                candidate.IsSelected = candidate.Id == profileId;
            }

            SelectedProfile = profile;
            SelectedCharacterData = null;
            RaiseStateChanged();
        }

        await LoadSelectedProfileAsync(profile, cancellationToken);
    }

    public async Task RemoveProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        CharacterProfile? profile = profiles.FirstOrDefault(candidate => candidate.Id == profileId);
        if (profile is null)
        {
            return;
        }

        bool wasSelected = profile.IsSelected;
        profiles.Remove(profile);

        if (!profiles.Any())
        {
            SelectedProfile = null;
            SelectedCharacterData = null;
            RaiseStateChanged();
            return;
        }

        if (!wasSelected)
        {
            RaiseStateChanged();
            return;
        }

        await SelectProfileAsync(profiles[0].Id, cancellationToken);
    }

    private async Task LoadSelectedProfileAsync(CharacterProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            CharacterData characterData = await characterConfigDataService.LoadAsync(profile, cancellationToken);
            profile.LastLoadedAt = characterData.LoadedAt;
            SelectedCharacterData = characterData;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load character profile {ProfileId}.", profile.Id);
            profile.LastLoadedAt = DateTimeOffset.Now;
            SelectedCharacterData = new CharacterData
            {
                Profile = profile,
                HotbarAnalysisResult = new HotbarAnalysisResult
                {
                    FilePath = CharacterConfigPathResolver.ResolveHotbarDatPath(profile),
                    Exists = false,
                },
                KeybindAnalysisResult = new KeybindAnalysisResult
                {
                    FilePath = CharacterConfigPathResolver.ResolveKeybindDatPath(profile),
                    Exists = false,
                },
                LoadedAt = profile.LastLoadedAt.Value,
                Errors = [$"キャラクター設定の読み込みに失敗しました: {exception.Message}"],
            };
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
