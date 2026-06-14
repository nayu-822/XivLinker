using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Services;
using XivLinker.App.ViewModels;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LuminaOptions>()
            .Bind(configuration.GetSection("Lumina"));
        services.AddOptions<OverlayPluginOptions>()
            .Bind(configuration.GetSection("OverlayPlugin"));

        services.AddSingleton<IGameDataService, LuminaGameDataService>();
        services.AddSingleton<IOverlayPluginWebSocketService, OverlayPluginWebSocketService>();
        services.AddSingleton<IOverlayPluginWebSocketSessionService, OverlayPluginWebSocketSessionService>();
        services.AddSingleton<OverlayPluginConnectionStateService>();
        services.AddSingleton<ICraftSequenceStore, CraftSequenceStore>();

        services.AddSingleton<AppEventLogViewModel>();
        services.AddSingleton<DashboardStatusViewModel>();
        services.AddSingleton<DataSourceStatusViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<AutoCraftViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
