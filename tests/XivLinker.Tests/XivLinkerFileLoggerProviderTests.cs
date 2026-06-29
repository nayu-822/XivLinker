using Microsoft.Extensions.Logging;
using XivLinker.App.Logging;
using XivLinker.Application.Logging;
using XivLinker.Application.Services;

namespace XivLinker.Tests;

public sealed class XivLinkerFileLoggerProviderTests
{
    [Fact]
    public void DefaultInfoLevel_EnablesInfoAndAbove()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = CreateContext(rootPath);

            Assert.False(context.Provider.IsEnabled(LogLevel.Debug));
            Assert.True(context.Provider.IsEnabled(LogLevel.Information));
            Assert.True(context.Provider.IsEnabled(LogLevel.Warning));
            Assert.True(context.Provider.IsEnabled(LogLevel.Error));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task SaveWarnLevel_DisablesInfoAndKeepsWarnEnabled()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = CreateContext(rootPath);

            await context.SettingsStore.SaveAsync(new() { FileLogLevel = XivLinkerLogLevel.Warn });

            Assert.False(context.Provider.IsEnabled(LogLevel.Information));
            Assert.True(context.Provider.IsEnabled(LogLevel.Warning));
            Assert.True(context.Provider.IsEnabled(LogLevel.Error));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task SaveDebugLevel_EnablesDebugAndTrace()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = CreateContext(rootPath);

            await context.SettingsStore.SaveAsync(new() { FileLogLevel = XivLinkerLogLevel.Debug });

            Assert.True(context.Provider.IsEnabled(LogLevel.Trace));
            Assert.True(context.Provider.IsEnabled(LogLevel.Debug));
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
        XivLinkerFileLogWriter writer = new(new FileLogOptions { LogsPath = pathService.LogsPath });
        XivLinkerFileLoggerProvider provider = new(settingsStore, writer);
        return new TestContext(settingsStore, provider, writer);
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
            XivLinkerFileLogWriter writer)
        {
            SettingsStore = settingsStore;
            Provider = provider;
            Writer = writer;
        }

        public AppSettingsStore SettingsStore { get; }

        public XivLinkerFileLoggerProvider Provider { get; }

        public XivLinkerFileLogWriter Writer { get; }

        public void Dispose()
        {
            Provider.Dispose();
            Writer.Dispose();
        }
    }
}
