using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XivLinker.App.DependencyInjection;
using XivLinker.App.Logging;
using XivLinker.App.ViewModels;
using XivLinker.Application.Abstractions;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    private readonly CancellationTokenSource startupInitializationCancellationTokenSource = new();
    private XivLinkerFileLoggerProvider? fileLoggerProvider;
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
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
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

        ILoggerFactory loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        IAppSettingsStore appSettingsStore = _host.Services.GetRequiredService<IAppSettingsStore>();
        XivLinkerLogWriterSet writerSet = _host.Services.GetRequiredService<XivLinkerLogWriterSet>();
        fileLoggerProvider ??= new XivLinkerFileLoggerProvider(appSettingsStore, writerSet);
        loggerFactory.AddProvider(fileLoggerProvider);
        await appSettingsStore.LoadAsync();
        await _host.StartAsync();

        ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("XivLinker を起動しました。");
        logger.LogInformation("データソース初期化を開始します。");

        MainViewModel mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

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
            logger?.LogInformation("XivLinker の終了処理を開始します。");

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
                logger?.LogInformation("XivLinker を終了しました。");
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
            fileLoggerProvider?.Dispose();
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
            logger.LogInformation("データソース初期化が完了しました。");
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
            logger?.LogError(e.Exception, "未処理の UI 例外が発生しました。");
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
            logger?.LogCritical(exception, "未処理のバックグラウンド例外が発生しました。");
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
            logger?.LogError(e.Exception, "未観測のバックグラウンド例外が発生しました。");
        }
        catch
        {
            // Logging should not throw while handling a background task exception.
        }

        e.SetObserved();
    }
}
