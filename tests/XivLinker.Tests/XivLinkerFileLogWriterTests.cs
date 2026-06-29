using Microsoft.Extensions.Logging;
using XivLinker.App.Logging;
using XivLinker.Application.Services;

namespace XivLinker.Tests;

public sealed class XivLinkerFileLogWriterTests
{
    [Fact]
    public async Task EnqueueAndFlushAsync_WritesToDailyLogFile()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            string logPath = Path.Combine(pathService.LogsPath, "xivlinker-20260629.log");

            using (XivLinkerFileLogWriter writer = new(new FileLogOptions { LogsPath = pathService.LogsPath }))
            {
                DateTime timestamp = new(2026, 6, 29, 12, 34, 56);
                writer.Enqueue(timestamp, "first line");
                await writer.FlushAsync();
            }

            string text = await File.ReadAllTextAsync(logPath);

            Assert.Contains("first line", text);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task Enqueue_DifferentDates_WritesSeparateFiles()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            using (XivLinkerFileLogWriter writer = new(new FileLogOptions { LogsPath = pathService.LogsPath }))
            {
                writer.Enqueue(new DateTime(2026, 6, 29, 1, 0, 0), "day1");
                writer.Enqueue(new DateTime(2026, 6, 30, 1, 0, 0), "day2");
                await writer.FlushAsync();
            }

            Assert.True(File.Exists(Path.Combine(pathService.LogsPath, "xivlinker-20260629.log")));
            Assert.True(File.Exists(Path.Combine(pathService.LogsPath, "xivlinker-20260630.log")));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task Logger_WritesCriticalAsErrorLabel()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            AppSettingsStore settingsStore = new(pathService);
            string logPath = Path.Combine(pathService.LogsPath, $"xivlinker-{DateTime.Now:yyyyMMdd}.log");

            using (XivLinkerFileLogWriter writer = new(new FileLogOptions { LogsPath = pathService.LogsPath }))
            using (XivLinkerFileLoggerProvider provider = new(settingsStore, writer))
            {
                ILogger logger = provider.CreateLogger("Test.Category");
                logger.LogCritical("critical message");
                await writer.FlushAsync();
            }

            string text = await File.ReadAllTextAsync(logPath);

            Assert.Contains("[ERROR] Test.Category - critical message", text);
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
