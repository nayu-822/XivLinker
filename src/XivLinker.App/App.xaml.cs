using System.Windows;
using System.Windows.Threading;
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

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        await _host.StartAsync();

        MainViewModel mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
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
        ILogger<App>? logger = null;

        try
        {
            startupInitializationCancellationTokenSource.Cancel();
            logger = _host.Services.GetService<ILogger<App>>();

            if (startupInitializationTask is not null)
            {
                try
                {
                    await startupInitializationTask;
                }
                catch (OperationCanceledException)
                {
                    // アプリ終了またはキャンセル時はエラー扱いしない
                }
            }

            OverlayPluginConnectionStateService overlayPluginConnectionStateService =
                _host.Services.GetRequiredService<OverlayPluginConnectionStateService>();

            try
            {
                await overlayPluginConnectionStateService.StopAsync();
            }
            catch (OperationCanceledException)
            {
                // アプリ終了またはキャンセル時はエラー扱いしない
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "終了時の OverlayPlugin 停止処理に失敗しました。");
            }

            try
            {
                await _host.StopAsync();
            }
            catch (OperationCanceledException)
            {
                // アプリ終了またはキャンセル時はエラー扱いしない
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "終了時のホスト停止処理に失敗しました。");
            }
        }
        finally
        {
            startupInitializationCancellationTokenSource.Dispose();
            _host.Dispose();
            base.OnExit(e);
        }
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
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            ILogger<App>? logger = _host.Services.GetService<ILogger<App>>();
            logger?.LogError(e.Exception, "Unhandled UI exception occurred.");
        }
        catch
        {
            // Logging should not throw while handling a UI exception.
        }

        MessageBox.Show(
            MainWindow,
            "アプリで未処理のエラーが発生しました。詳細はログを確認してください。",
            "XivLinker",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is not Exception exception)
        {
            return;
        }

        try
        {
            ILogger<App>? logger = _host.Services.GetService<ILogger<App>>();
            logger?.LogCritical(exception, "AppDomain unhandled exception occurred.");
        }
        catch
        {
            // Logging should not throw while handling a process-level exception.
        }
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            ILogger<App>? logger = _host.Services.GetService<ILogger<App>>();
            logger?.LogError(e.Exception, "Unobserved task exception occurred.");
        }
        catch
        {
            // Logging should not throw while handling a background task exception.
        }

        e.SetObserved();
    }
}
