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
    private CharacterProfile? selectedProfile;
    private CharacterData? selectedCharacterData;

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

    public IReadOnlyList<CharacterProfile> Profiles => profiles.Select(CloneProfile).ToArray();

    public CharacterProfile? SelectedProfile => selectedProfile is null ? null : CloneProfile(selectedProfile);

    public CharacterData? SelectedCharacterData => selectedCharacterData is null ? null : CloneCharacterData(selectedCharacterData);

    public string? SelectedCharacterDirectoryPath => selectedProfile?.CharacterSettingsDirectory;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (selectedProfile is null)
        {
            RaiseStateChanged();
            return;
        }

        await LoadSelectedProfileAsync(selectedProfile, cancellationToken);
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

        selectedProfile = selected;
        selectedCharacterData = null;
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

        if (!ReferenceEquals(selectedProfile, profile))
        {
            foreach (CharacterProfile candidate in profiles)
            {
                candidate.IsSelected = candidate.Id == profileId;
            }

            selectedProfile = profile;
            selectedCharacterData = null;
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
            selectedProfile = null;
            selectedCharacterData = null;
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
                selectedProfile = profiles.FirstOrDefault(profile => profile.Id == selectedProfileId);
            }
            else
            {
                selectedProfile = profiles.FirstOrDefault(profile => profile.IsSelected);
            }

            if (selectedProfile is not null)
            {
                foreach (CharacterProfile profile in profiles)
                {
                    profile.IsSelected = profile.Id == selectedProfile.Id;
                }
            }
        }
        catch (JsonException exception)
        {
            BackupBrokenFile(path);
            logger.LogError(exception, "Failed to parse character profile store file.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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
            selectedCharacterData = CloneCharacterData(characterData);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load character profile {ProfileId}.", profile.Id);
            profile.LastLoadedAt = DateTimeOffset.Now;
            selectedCharacterData = new CharacterData
            {
                Profile = CloneProfile(profile),
                CharacterDirectoryPath = profile.CharacterSettingsDirectory,
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
                SelectedProfileId = selectedProfile?.Id,
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Failed to persist character profiles.");
        }
    }

    private void BackupBrokenFile(string path)
    {
        try
        {
            string backupPath = $"{path}.{DateTimeOffset.Now:yyyyMMddHHmmss}.bak";
            File.Move(path, backupPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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

    private static CharacterProfile CloneProfile(CharacterProfile profile)
    {
        return new CharacterProfile
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            CharacterSettingsDirectory = profile.CharacterSettingsDirectory,
            HotbarDatPath = profile.HotbarDatPath,
            KeybindDatPath = profile.KeybindDatPath,
            IsSelected = profile.IsSelected,
            LastLoadedAt = profile.LastLoadedAt,
        };
    }

    private static CharacterData CloneCharacterData(CharacterData characterData)
    {
        return new CharacterData
        {
            Profile = CloneProfile(characterData.Profile),
            CharacterDirectoryPath = characterData.CharacterDirectoryPath,
            HotbarAnalysisResult = new HotbarAnalysisResult
            {
                FilePath = characterData.HotbarAnalysisResult.FilePath,
                Exists = characterData.HotbarAnalysisResult.Exists,
                ByteLength = characterData.HotbarAnalysisResult.ByteLength,
                LastWriteTime = characterData.HotbarAnalysisResult.LastWriteTime,
                RawBytes = characterData.HotbarAnalysisResult.RawBytes?.ToArray(),
            },
            KeybindAnalysisResult = new KeybindAnalysisResult
            {
                FilePath = characterData.KeybindAnalysisResult.FilePath,
                Exists = characterData.KeybindAnalysisResult.Exists,
                ByteLength = characterData.KeybindAnalysisResult.ByteLength,
                LastWriteTime = characterData.KeybindAnalysisResult.LastWriteTime,
                RawBytes = characterData.KeybindAnalysisResult.RawBytes?.ToArray(),
            },
            LoadedAt = characterData.LoadedAt,
            Errors = characterData.Errors.ToArray(),
        };
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
