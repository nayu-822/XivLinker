using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XivLinker.App.DependencyInjection;
using XivLinker.App.ViewModels;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    private readonly CancellationTokenSource startupInitializationCancellationTokenSource = new();
    private Task? startupInitializationTask;

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
        startupInitializationTask = InitializeDataSourcesAsync(
            mainViewModel,
            logger,
            startupInitializationCancellationTokenSource.Token);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        startupInitializationCancellationTokenSource.Cancel();

        if (startupInitializationTask is not null)
        {
            try
            {
                await startupInitializationTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        OverlayPluginConnectionStateService overlayPluginConnectionStateService =
            _host.Services.GetRequiredService<OverlayPluginConnectionStateService>();
        await overlayPluginConnectionStateService.StopAsync();

        await _host.StopAsync();
        startupInitializationCancellationTokenSource.Dispose();
        _host.Dispose();

        base.OnExit(e);
    }

    private static async Task InitializeDataSourcesAsync(
        MainViewModel mainViewModel,
        ILogger<App> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await mainViewModel.InitializeAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // アプリ終了またはキャンセル時はエラー扱いしない
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "起動時のデータソース初期化に失敗しました。");
        }
    }
}
