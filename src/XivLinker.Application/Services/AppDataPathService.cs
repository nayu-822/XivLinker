using XivLinker.Application.Abstractions;

namespace XivLinker.Application.Services;

public sealed class AppDataPathService : IAppDataPathService
{
    public AppDataPathService()
        : this(null)
    {
    }

    public AppDataPathService(string? rootPath)
    {
        AppDataRootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XivLinker")
            : Path.GetFullPath(rootPath);

        CacheRootPath = Path.Combine(AppDataRootPath, "Cache");
        IconCachePath = Path.Combine(CacheRootPath, "Icons");
        LogsPath = Path.Combine(AppDataRootPath, "Logs");
        SettingsFilePath = Path.Combine(AppDataRootPath, "settings.json");
        CharacterProfilesFilePath = Path.Combine(AppDataRootPath, "character-profiles.json");
        CraftSequencesFilePath = Path.Combine(AppDataRootPath, "craft-sequences.json");

        Directory.CreateDirectory(AppDataRootPath);
        Directory.CreateDirectory(CacheRootPath);
        Directory.CreateDirectory(IconCachePath);
        Directory.CreateDirectory(LogsPath);
    }

    public string AppDataRootPath { get; }

    public string SettingsFilePath { get; }

    public string CharacterProfilesFilePath { get; }

    public string CraftSequencesFilePath { get; }

    public string CacheRootPath { get; }

    public string IconCachePath { get; }

    public string LogsPath { get; }
}
