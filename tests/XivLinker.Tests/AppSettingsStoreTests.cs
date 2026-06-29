using XivLinker.Application.Logging;
using XivLinker.Application.Services;

namespace XivLinker.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_WithoutSettingsFile_ReturnsDefaultLogLevels()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppSettingsStore store = new(new AppDataPathService(rootPath));

            var settings = await store.LoadAsync();

            Assert.Equal(XivLinkerLogLevel.Info, settings.FileLogLevel);
            Assert.Equal(XivLinkerLogLevel.Warn, settings.WebSocketLogLevel);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task SaveAsync_PersistsFileLogLevel()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppSettingsStore store = new(new AppDataPathService(rootPath));

            await store.SaveAsync(new()
            {
                FileLogLevel = XivLinkerLogLevel.Warn,
                WebSocketLogLevel = XivLinkerLogLevel.Debug,
            });

            AppSettingsStore reloaded = new(new AppDataPathService(rootPath));
            var settings = await reloaded.LoadAsync();

            Assert.Equal(XivLinkerLogLevel.Warn, settings.FileLogLevel);
            Assert.Equal(XivLinkerLogLevel.Debug, settings.WebSocketLogLevel);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_BacksUpFileAndFallsBackToDefault()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            Directory.CreateDirectory(rootPath);
            await File.WriteAllTextAsync(pathService.SettingsFilePath, "{ invalid json");

            AppSettingsStore store = new(pathService);
            var settings = await store.LoadAsync();

            Assert.Equal(XivLinkerLogLevel.Info, settings.FileLogLevel);
            Assert.Equal(XivLinkerLogLevel.Warn, settings.WebSocketLogLevel);
            Assert.False(File.Exists(pathService.SettingsFilePath));
            Assert.Single(Directory.EnumerateFiles(rootPath, "settings.invalid-*.json"));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task SaveAsync_RaisesSettingsChanged()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppSettingsStore store = new(new AppDataPathService(rootPath));
            int raisedCount = 0;
            store.SettingsChanged += (_, _) => raisedCount++;

            await store.SaveAsync(new() { FileLogLevel = XivLinkerLogLevel.Error });

            Assert.Equal(1, raisedCount);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public void Current_ReturnsCopy()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppSettingsStore store = new(new AppDataPathService(rootPath));

            var snapshot = store.Current;
            snapshot.FileLogLevel = XivLinkerLogLevel.Error;
            snapshot.WebSocketLogLevel = XivLinkerLogLevel.Debug;

            Assert.Equal(XivLinkerLogLevel.Info, store.Current.FileLogLevel);
            Assert.Equal(XivLinkerLogLevel.Warn, store.Current.WebSocketLogLevel);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    private static string CreateAppDataRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "XivLinkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
}
