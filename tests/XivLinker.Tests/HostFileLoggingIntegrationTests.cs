using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XivLinker.App.Logging;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Services;

namespace XivLinker.Tests;

public sealed class HostFileLoggingIntegrationTests
{
    [Fact]
    public async Task HostLogging_WritesILoggerMessagesToFile()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            AppDataPathService pathService = new(rootPath);
            string logPath = Path.Combine(pathService.LogsPath, $"xivlinker-{DateTime.Now:yyyyMMdd}.log");

            using (IHost host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(configuration => configuration.Sources.Clear())
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IAppDataPathService>(pathService);
                    services.AddSingleton<IAppSettingsStore>(serviceProvider =>
                        new AppSettingsStore(
                            serviceProvider.GetRequiredService<IAppDataPathService>(),
                            serviceProvider.GetRequiredService<ILogger<AppSettingsStore>>()));
                    services.AddSingleton(_ => new XivLinkerLogWriterSet(
                        new XivLinkerFileLogWriter(new FileLogOptions
                        {
                            LogsPath = pathService.LogsPath,
                            FilePrefix = "xivlinker",
                        }),
                        new XivLinkerFileLogWriter(new FileLogOptions
                        {
                            LogsPath = pathService.LogsPath,
                            FilePrefix = "websocket",
                        })));
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddDebug();
                })
                .Build())
            {
                ILoggerFactory loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
                IAppSettingsStore settingsStore = host.Services.GetRequiredService<IAppSettingsStore>();
                XivLinkerLogWriterSet writerSet = host.Services.GetRequiredService<XivLinkerLogWriterSet>();
                using XivLinkerFileLoggerProvider provider = new(settingsStore, writerSet);
                loggerFactory.AddProvider(provider);

                await settingsStore.LoadAsync();
                await host.StartAsync();

                ILogger<HostFileLoggingIntegrationTests> logger = host.Services.GetRequiredService<ILogger<HostFileLoggingIntegrationTests>>();
                logger.LogInformation("host-file-log");

                await host.StopAsync();
            }
            string text = await File.ReadAllTextAsync(logPath);

            Assert.Contains("host-file-log", text);
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
