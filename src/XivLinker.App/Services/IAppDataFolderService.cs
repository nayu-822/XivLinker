namespace XivLinker.App.Services;

public interface IAppDataFolderService
{
    string AppDataRootPath { get; }

    string CachePath { get; }

    string IconCachePath { get; }

    string LogsPath { get; }

    Task OpenFolderAsync(string path, CancellationToken cancellationToken = default);

    Task<AppDataFolderStats> GetStatsAsync(CancellationToken cancellationToken = default);

    Task DeleteIconCacheAsync(CancellationToken cancellationToken = default);

    Task DeleteLogFilesAsync(CancellationToken cancellationToken = default);

    Task DeleteAllCacheAsync(CancellationToken cancellationToken = default);
}
