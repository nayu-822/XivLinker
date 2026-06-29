using Microsoft.Extensions.Logging;
using XivLinker.App.Logging;
using XivLinker.Application.Logging;
using XivLinker.Application.Services;

namespace XivLinker.Tests;

public sealed class XivLinkerFileLoggerProviderTests
{
    [Fact]
    public void DefaultLevels_EnableAppInfoAndWebSocketWarn()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = CreateContext(rootPath);

            Assert.True(context.Provider.IsEnabled("XivLinker.App.App", LogLevel.Information));
            Assert.False(context.Provider.IsEnabled("XivLinker.App.App", LogLevel.Debug));
            Assert.True(context.Provider.IsEnabled("XivLinker.Infrastructure.Overlay.Services.Client", LogLevel.Warning));
            Assert.False(context.Provider.IsEnabled("XivLinker.Infrastructure.Overlay.Services.Client", LogLevel.Information));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task SavingSettings_UpdatesAppAndWebSocketThresholdsIndependently()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = CreateContext(rootPath);

            await context.SettingsStore.SaveAsync(new()
            {
                FileLogLevel = XivLinkerLogLevel.Error,
                WebSocketLogLevel = XivLinkerLogLevel.Debug,
            });

            Assert.False(context.Provider.IsEnabled("XivLinker.App.App", LogLevel.Warning));
            Assert.True(context.Provider.IsEnabled("XivLinker.App.App", LogLevel.Error));
            Assert.True(context.Provider.IsEnabled("XivLinker.Infrastructure.Overlay.Services.Client", LogLevel.Debug));
            Assert.True(context.Provider.IsEnabled("XivLinker.Infrastructure.Overlay.Services.Client", LogLevel.Trace));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    private static TestContext CreateContext(string rootPath)
    {
        AppDataPathService pathService = new(rootPath);
        AppSettingsStore settingsStore = new(pathService);
        XivLinkerLogWriterSet writerSet = new(
            new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "xivlinker" }),
            new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" }));
        XivLinkerFileLoggerProvider provider = new(settingsStore, writerSet);
        return new TestContext(settingsStore, provider, writerSet);
    }

    private static string CreateAppDataRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "XivLinkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(
            AppSettingsStore settingsStore,
            XivLinkerFileLoggerProvider provider,
            XivLinkerLogWriterSet writerSet)
        {
            SettingsStore = settingsStore;
            Provider = provider;
            WriterSet = writerSet;
        }

        public AppSettingsStore SettingsStore { get; }

        public XivLinkerFileLoggerProvider Provider { get; }

        public XivLinkerLogWriterSet WriterSet { get; }

        public void Dispose()
        {
            Provider.Dispose();
            WriterSet.Dispose();
        }
    }
}
