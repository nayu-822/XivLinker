using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        await mainViewModel.InitializeDataSourcesAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
