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
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IAppDataPathService>(pathService);
                    services.AddSingleton<IAppSettingsStore>(_ => new AppSettingsStore(pathService));
                    services.AddSingleton(new FileLogOptions
                    {
                        LogsPath = pathService.LogsPath,
                    });
                    services.AddSingleton<XivLinkerFileLogWriter>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.Services.AddSingleton<ILoggerProvider, XivLinkerFileLoggerProvider>();
                })
                .Build())
            {
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
