using XivLinker.App.Services;
using XivLinker.Application.Services;

namespace XivLinker.Tests;

public sealed class AppDataFolderServiceTests
{
    [Fact]
    public async Task GetStatsAsync_ReturnsManagedFolderSummaries()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataFolderService service = CreateService(rootPath);
            await File.WriteAllBytesAsync(Path.Combine(service.IconCachePath, "000001.png"), [0x01, 0x02, 0x03]);
            await File.WriteAllBytesAsync(Path.Combine(service.LogsPath, "latest.log"), [0x0A, 0x0B]);

            AppDataFolderStats stats = await service.GetStatsAsync();

            Assert.Equal(rootPath, service.AppDataRootPath);
            Assert.Equal(1, stats.IconCache.FileCount);
            Assert.Equal(3, stats.IconCache.TotalBytes);
            Assert.Equal(1, stats.Logs.FileCount);
            Assert.Equal(2, stats.Logs.TotalBytes);
            Assert.True(stats.Cache.Exists);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task DeleteIconCacheAsync_RemovesOnlyIconCacheContents()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataFolderService service = CreateService(rootPath);
            string iconPath = Path.Combine(service.IconCachePath, "000001.png");
            string logPath = Path.Combine(service.LogsPath, "latest.log");
            await File.WriteAllBytesAsync(iconPath, [0x01]);
            await File.WriteAllBytesAsync(logPath, [0x02]);

            await service.DeleteIconCacheAsync();

            Assert.Empty(Directory.EnumerateFiles(service.IconCachePath, "*", SearchOption.AllDirectories));
            Assert.True(File.Exists(logPath));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task DeleteLogFilesAsync_RemovesNestedLogFiles()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataFolderService service = CreateService(rootPath);
            string archiveDirectory = Path.Combine(service.LogsPath, "archive");
            Directory.CreateDirectory(archiveDirectory);
            string logPath = Path.Combine(archiveDirectory, "latest.txt");
            await File.WriteAllBytesAsync(logPath, [0x01, 0x02]);

            await service.DeleteLogFilesAsync();

            Assert.Empty(Directory.EnumerateFiles(service.LogsPath, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task DeleteAllCacheAsync_DoesNotTouchFilesOutsideManagedCache()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataFolderService service = CreateService(rootPath);
            string cacheFilePath = Path.Combine(service.IconCachePath, "000001.png");
            string settingsFilePath = Path.Combine(service.AppDataRootPath, "settings.json");
            await File.WriteAllBytesAsync(cacheFilePath, [0x01]);
            await File.WriteAllBytesAsync(settingsFilePath, [0x02]);

            await service.DeleteAllCacheAsync();

            Assert.False(File.Exists(cacheFilePath));
            Assert.True(File.Exists(settingsFilePath));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    private static AppDataFolderService CreateService(string rootPath)
    {
        return new AppDataFolderService(new AppDataPathService(rootPath));
    }

    private static string CreateAppDataRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "XivLinkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
}
