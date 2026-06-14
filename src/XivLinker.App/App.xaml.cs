using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XivLinker.App.DependencyInjection;
using XivLinker.App.ViewModels;

namespace XivLinker.App;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.Sources.Clear();
                configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplicationServices(context.Configuration);
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        await _host.StartAsync();

        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
        _ = InitializeDataSourcesAsync(mainViewModel, logger);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }

    private static async Task InitializeDataSourcesAsync(
        MainViewModel mainViewModel,
        ILogger<App> logger)
    {
        try
        {
            await mainViewModel.InitializeDataSourcesAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "\u8D77\u52D5\u6642\u306E\u30C7\u30FC\u30BF\u30BD\u30FC\u30B9\u521D\u671F\u5316\u306B\u5931\u6557\u3057\u307E\u3057\u305F\u3002");
        }
    }
}
