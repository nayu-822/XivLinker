using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Application.Abstractions;

namespace XivLinker.App.Services;

public sealed class AppDataFolderService : IAppDataFolderService
{
    private readonly IAppDataPathService appDataPathService;
    private readonly ILogger<AppDataFolderService> logger;

    public AppDataFolderService(
        IAppDataPathService appDataPathService,
        ILogger<AppDataFolderService>? logger = null)
    {
        this.appDataPathService = appDataPathService;
        this.logger = logger ?? NullLogger<AppDataFolderService>.Instance;
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
        logger.LogInformation("アプリデータフォルダを開きます。Path={Path}", path);

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
            try
            {
                FileInfo fileInfo = new(filePath);
                totalBytes += fileInfo.Length;
                fileCount++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "アクセスできないファイルを集計から除外しました。Path={Path}", filePath);
            }
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

                int deletedFileCount = 0;
                int skippedFileCount = 0;

                foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        File.SetAttributes(filePath, FileAttributes.Normal);
                        File.Delete(filePath);
                        deletedFileCount++;
                    }
                    catch (IOException exception)
                    {
                        skippedFileCount++;
                        logger.LogWarning(exception, "ファイルを削除できなかったためスキップしました。Path={Path}", filePath);
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        skippedFileCount++;
                        logger.LogWarning(exception, "ファイルを削除できなかったためスキップしました。Path={Path}", filePath);
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
                    catch (IOException exception)
                    {
                        logger.LogDebug(exception, "削除対象ディレクトリを整理できませんでした。Path={Path}", childDirectory);
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        logger.LogDebug(exception, "削除対象ディレクトリを整理できませんでした。Path={Path}", childDirectory);
                    }
                }

                logger.LogInformation(
                    "ディレクトリ配下のファイル削除を完了しました。Path={Path}, DeletedFiles={DeletedFiles}, SkippedFiles={SkippedFiles}",
                    directoryPath,
                    deletedFileCount,
                    skippedFileCount);
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
