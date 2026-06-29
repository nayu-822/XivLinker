using System.Diagnostics;
using System.IO;
using XivLinker.Application.Abstractions;

namespace XivLinker.App.Services;

public sealed class AppDataFolderService : IAppDataFolderService
{
    private readonly IAppDataPathService appDataPathService;

    public AppDataFolderService(IAppDataPathService appDataPathService)
    {
        this.appDataPathService = appDataPathService;
    }

    public string AppDataRootPath => appDataPathService.AppDataRootPath;

    public string CachePath => appDataPathService.CacheRootPath;

    public string IconCachePath => appDataPathService.IconCachePath;

    public string LogsPath => appDataPathService.LogsPath;

    public Task OpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        EnsureManagedPath(path);
        Directory.CreateDirectory(path);

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });

        return Task.CompletedTask;
    }

    public Task<AppDataFolderStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => new AppDataFolderStats(
                CreateStats("アイコンキャッシュ", IconCachePath, cancellationToken),
                CreateStats("ログファイル", LogsPath, cancellationToken),
                CreateStats("キャッシュ全体", CachePath, cancellationToken)),
            cancellationToken);
    }

    public Task DeleteIconCacheAsync(CancellationToken cancellationToken = default)
    {
        return DeleteDirectoryContentsAsync(IconCachePath, cancellationToken);
    }

    public Task DeleteLogFilesAsync(CancellationToken cancellationToken = default)
    {
        return DeleteDirectoryContentsAsync(LogsPath, cancellationToken);
    }

    public Task DeleteAllCacheAsync(CancellationToken cancellationToken = default)
    {
        return DeleteDirectoryContentsAsync(CachePath, cancellationToken);
    }

    private AppDataStorageCategoryStats CreateStats(
        string displayName,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(path))
        {
            return new AppDataStorageCategoryStats(displayName, path, 0, 0, Exists: false);
        }

        long totalBytes = 0;
        int fileCount = 0;

        foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo fileInfo = new(filePath);
            totalBytes += fileInfo.Length;
            fileCount++;
        }

        return new AppDataStorageCategoryStats(displayName, path, totalBytes, fileCount, Exists: true);
    }

    private async Task DeleteDirectoryContentsAsync(string directoryPath, CancellationToken cancellationToken)
    {
        await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureManagedPath(directoryPath);

                if (!Directory.Exists(directoryPath))
                {
                    return;
                }

                foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        File.SetAttributes(filePath, FileAttributes.Normal);
                        File.Delete(filePath);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                foreach (string childDirectory in Directory
                             .EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories)
                             .OrderByDescending(static path => path.Length))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(childDirectory).Any())
                        {
                            Directory.Delete(childDirectory);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            },
            cancellationToken);
    }

    private void EnsureManagedPath(string path)
    {
        string rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppDataRootPath));
        string targetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        if (targetPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string prefix = rootPath + Path.DirectorySeparatorChar;
        if (!targetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("XivLinker のアプリデータ配下のみ操作できます。");
        }
    }
}

public sealed record AppDataFolderStats(
    AppDataStorageCategoryStats IconCache,
    AppDataStorageCategoryStats Logs,
    AppDataStorageCategoryStats Cache);

public sealed record AppDataStorageCategoryStats(
    string DisplayName,
    string Path,
    long TotalBytes,
    int FileCount,
    bool Exists);
