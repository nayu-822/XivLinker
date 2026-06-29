using Microsoft.Extensions.Logging;
using XivLinker.App.Logging;
using XivLinker.Application.Logging;
using XivLinker.Application.Services;

namespace XivLinker.Tests;

public sealed class XivLinkerFileLogWriterTests
{
    [Fact]
    public async Task EnqueueAndFlushAsync_WritesToConfiguredDailyLogFile()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            string logPath = Path.Combine(pathService.LogsPath, "websocket-20260629.log");

            using (XivLinkerFileLogWriter writer = new(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" }))
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
            using (XivLinkerFileLogWriter writer = new(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "xivlinker" }))
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
    public async Task Logger_RoutesOverlayWarningsToWebSocketLogOnly()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            AppSettingsStore settingsStore = new(pathService);
            string appLogPath = Path.Combine(pathService.LogsPath, $"xivlinker-{DateTime.Now:yyyyMMdd}.log");
            string webSocketLogPath = Path.Combine(pathService.LogsPath, $"websocket-{DateTime.Now:yyyyMMdd}.log");

            using (XivLinkerLogWriterSet writerSet = new(
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "xivlinker" }),
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" })))
            using (XivLinkerFileLoggerProvider provider = new(settingsStore, writerSet))
            {
                ILogger logger = provider.CreateLogger("XivLinker.Infrastructure.Overlay.Services.TestClient");
                logger.LogWarning("websocket warning");
                await writerSet.WebSocketWriter.FlushAsync();
                await writerSet.AppWriter.FlushAsync();
            }

            string webSocketText = await File.ReadAllTextAsync(webSocketLogPath);
            Assert.Contains("[WARN] XivLinker.Infrastructure.Overlay.Services.TestClient - websocket warning", webSocketText);

            if (File.Exists(appLogPath))
            {
                string appText = await File.ReadAllTextAsync(appLogPath);
                Assert.DoesNotContain("websocket warning", appText);
            }
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task Logger_WritesAppInfoToAppLog()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            AppSettingsStore settingsStore = new(pathService);
            string appLogPath = Path.Combine(pathService.LogsPath, $"xivlinker-{DateTime.Now:yyyyMMdd}.log");

            using (XivLinkerLogWriterSet writerSet = new(
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "xivlinker" }),
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" })))
            using (XivLinkerFileLoggerProvider provider = new(settingsStore, writerSet))
            {
                ILogger logger = provider.CreateLogger("XivLinker.App.App");
                logger.LogInformation("app info");
                await writerSet.AppWriter.FlushAsync();
            }

            string appText = await File.ReadAllTextAsync(appLogPath);
            Assert.Contains("[INFO] XivLinker.App.App - app info", appText);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task Logger_DoesNotWriteOverlayInfoWhenWebSocketLevelIsWarn()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            AppSettingsStore settingsStore = new(pathService);
            string webSocketLogPath = Path.Combine(pathService.LogsPath, $"websocket-{DateTime.Now:yyyyMMdd}.log");

            using (XivLinkerLogWriterSet writerSet = new(
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "xivlinker" }),
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" })))
            using (XivLinkerFileLoggerProvider provider = new(settingsStore, writerSet))
            {
                ILogger logger = provider.CreateLogger("XivLinker.Infrastructure.Overlay.Services.TestClient");
                logger.LogInformation("filtered overlay info");
                await writerSet.WebSocketWriter.FlushAsync();
            }

            if (File.Exists(webSocketLogPath))
            {
                string webSocketText = await File.ReadAllTextAsync(webSocketLogPath);
                Assert.DoesNotContain("filtered overlay info", webSocketText);
            }
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task Logger_WritesOverlayDebugWhenWebSocketLevelIsDebug()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            AppSettingsStore settingsStore = new(pathService);
            string webSocketLogPath = Path.Combine(pathService.LogsPath, $"websocket-{DateTime.Now:yyyyMMdd}.log");

            await settingsStore.SaveAsync(new()
            {
                FileLogLevel = XivLinkerLogLevel.Info,
                WebSocketLogLevel = XivLinkerLogLevel.Debug,
            });

            using (XivLinkerLogWriterSet writerSet = new(
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "xivlinker" }),
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" })))
            using (XivLinkerFileLoggerProvider provider = new(settingsStore, writerSet))
            {
                ILogger logger = provider.CreateLogger("XivLinker.Infrastructure.Overlay.Services.TestClient");
                logger.LogDebug("payload json");
                await writerSet.WebSocketWriter.FlushAsync();
            }

            string webSocketText = await File.ReadAllTextAsync(webSocketLogPath);
            Assert.Contains("[DEBUG] XivLinker.Infrastructure.Overlay.Services.TestClient - payload json", webSocketText);
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

            using (XivLinkerLogWriterSet writerSet = new(
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "xivlinker" }),
                       new XivLinkerFileLogWriter(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" })))
            using (XivLinkerFileLoggerProvider provider = new(settingsStore, writerSet))
            {
                ILogger logger = provider.CreateLogger("Test.Category");
                logger.LogCritical("critical message");
                await writerSet.AppWriter.FlushAsync();
            }

            string text = await File.ReadAllTextAsync(logPath);

            Assert.Contains("[ERROR] Test.Category - critical message", text);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task Dispose_DrainsQueuedLogs()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            string logPath = Path.Combine(pathService.LogsPath, "websocket-20260629.log");

            using (XivLinkerFileLogWriter writer = new(new FileLogOptions { LogsPath = pathService.LogsPath, FilePrefix = "websocket" }))
            {
                writer.Enqueue(new DateTime(2026, 6, 29, 12, 0, 0), "dispose-drain");
            }

            string text = await File.ReadAllTextAsync(logPath);

            Assert.Contains("dispose-drain", text);
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
