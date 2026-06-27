using System.Text.Json;
using Microsoft.Extensions.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CharacterProfileStore : ICharacterProfileStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly ICharacterConfigDataService characterConfigDataService;
    private readonly IAppDataPathService appDataPathService;
    private readonly ILogger<CharacterProfileStore> logger;
    private readonly List<CharacterProfile> profiles = [];

    public CharacterProfileStore(
        ICharacterConfigDataService characterConfigDataService,
        IAppDataPathService appDataPathService,
        ILogger<CharacterProfileStore> logger)
    {
        this.characterConfigDataService = characterConfigDataService;
        this.appDataPathService = appDataPathService;
        this.logger = logger;
        LoadProfilesFromDisk();
    }

    public event EventHandler? StateChanged;

    public IReadOnlyList<CharacterProfile> Profiles => profiles;

    public CharacterProfile? SelectedProfile { get; private set; }

    public CharacterData? SelectedCharacterData { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProfile is null)
        {
            RaiseStateChanged();
            return;
        }

        await LoadSelectedProfileAsync(SelectedProfile, cancellationToken);
    }

    public async Task AddProfileAsync(string path, string? displayName = null, CancellationToken cancellationToken = default)
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
            DisplayName = NormalizeDisplayName(displayName, normalizedPath),
            CharacterSettingsDirectory = normalizedPath,
        };

        profiles.Add(profile);
        Persist();
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
        Persist();
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
            Persist();
            RaiseStateChanged();
        }

        await LoadSelectedProfileAsync(profile, cancellationToken);
    }

    public Task UpdateDisplayNameAsync(string profileId, string? displayName, CancellationToken cancellationToken = default)
    {
        CharacterProfile? profile = profiles.FirstOrDefault(candidate => candidate.Id == profileId);
        if (profile is null)
        {
            return Task.CompletedTask;
        }

        profile.DisplayName = NormalizeDisplayName(displayName, profile.CharacterSettingsDirectory);
        Persist();
        RaiseStateChanged();
        return Task.CompletedTask;
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
            Persist();
            RaiseStateChanged();
            return;
        }

        Persist();

        if (!wasSelected)
        {
            RaiseStateChanged();
            return;
        }

        await SelectProfileAsync(profiles[0].Id, cancellationToken);
    }

    private void LoadProfilesFromDisk()
    {
        string path = appDataPathService.CharacterProfilesFilePath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            CharacterProfileStoreDocument? document = JsonSerializer.Deserialize<CharacterProfileStoreDocument>(json, JsonOptions);
            if (document?.Profiles is null)
            {
                return;
            }

            profiles.Clear();

            foreach (CharacterProfileDocument profile in document.Profiles)
            {
                profiles.Add(new CharacterProfile
                {
                    Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id,
                    DisplayName = profile.DisplayName ?? NormalizeDisplayName(null, profile.CharacterSettingsDirectory ?? string.Empty),
                    CharacterSettingsDirectory = profile.CharacterSettingsDirectory ?? string.Empty,
                    HotbarDatPath = profile.HotbarDatPath,
                    KeybindDatPath = profile.KeybindDatPath,
                    IsSelected = profile.IsSelected,
                    LastLoadedAt = profile.LastLoadedAt,
                });
            }

            string? selectedProfileId = document.SelectedProfileId;
            if (!string.IsNullOrWhiteSpace(selectedProfileId))
            {
                SelectedProfile = profiles.FirstOrDefault(profile => profile.Id == selectedProfileId);
            }
            else
            {
                SelectedProfile = profiles.FirstOrDefault(profile => profile.IsSelected);
            }

            if (SelectedProfile is not null)
            {
                foreach (CharacterProfile profile in profiles)
                {
                    profile.IsSelected = profile.Id == SelectedProfile.Id;
                }
            }
        }
        catch (JsonException exception)
        {
            BackupBrokenFile(path);
            logger.LogError(exception, "Failed to parse character profile store file.");
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Failed to read character profile store file.");
        }
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

        Persist();
        RaiseStateChanged();
    }

    private void Persist()
    {
        try
        {
            var document = new CharacterProfileStoreDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                SelectedProfileId = SelectedProfile?.Id,
                Profiles = profiles.Select(profile => new CharacterProfileDocument
                {
                    Id = profile.Id,
                    DisplayName = profile.DisplayName,
                    CharacterSettingsDirectory = profile.CharacterSettingsDirectory,
                    HotbarDatPath = profile.HotbarDatPath,
                    KeybindDatPath = profile.KeybindDatPath,
                    IsSelected = profile.IsSelected,
                    LastLoadedAt = profile.LastLoadedAt,
                }).ToArray(),
            };

            WriteAtomically(appDataPathService.CharacterProfilesFilePath, JsonSerializer.Serialize(document, JsonOptions));
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Failed to persist character profiles.");
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Failed to persist character profiles.");
        }
    }

    private void BackupBrokenFile(string path)
    {
        try
        {
            string backupPath = $"{path}.{DateTimeOffset.Now:yyyyMMddHHmmss}.bak";
            File.Copy(path, backupPath, overwrite: true);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Failed to back up broken character profile store file.");
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tempPath, content);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
            return;
        }

        File.Move(tempPath, path);
    }

    private static string NormalizeDisplayName(string? displayName, string characterSettingsDirectory)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        string trimmedDirectory = characterSettingsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folderName = Path.GetFileName(trimmedDirectory);
        return string.IsNullOrWhiteSpace(folderName) ? "キャラクター設定" : folderName;
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class CharacterProfileStoreDocument
    {
        public int SchemaVersion { get; init; }

        public string? SelectedProfileId { get; init; }

        public CharacterProfileDocument[] Profiles { get; init; } = [];
    }

    private sealed class CharacterProfileDocument
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? CharacterSettingsDirectory { get; init; }

        public string? HotbarDatPath { get; init; }

        public string? KeybindDatPath { get; init; }

        public bool IsSelected { get; init; }

        public DateTimeOffset? LastLoadedAt { get; init; }
    }
}
