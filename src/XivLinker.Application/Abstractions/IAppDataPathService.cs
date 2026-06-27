namespace XivLinker.Application.Abstractions;

public interface IAppDataPathService
{
    string AppDataRootPath { get; }

    string SettingsFilePath { get; }

    string CharacterProfilesFilePath { get; }

    string CraftSequencesFilePath { get; }

    string CacheRootPath { get; }

    string IconCachePath { get; }
}
