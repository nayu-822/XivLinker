using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Services;
using XivLinker.App.Services;
using XivLinker.App.ViewModels;
using XivLinker.Infrastructure.CharacterConfig.Services;
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

        services.AddSingleton<IAppDataPathService, AppDataPathService>();
        services.AddSingleton<LuminaGameDataService>();
        services.AddSingleton<ILuminaGameDataProvider>(static serviceProvider => serviceProvider.GetRequiredService<LuminaGameDataService>());
        services.AddSingleton<LuminaActionIconService>();
        services.AddSingleton<ICrafterActionCatalogService, LuminaCrafterActionCatalogService>();
        services.AddSingleton<IGameDataService>(static serviceProvider => serviceProvider.GetRequiredService<LuminaGameDataService>());
        services.AddSingleton<IOverlayPluginWebSocketService, OverlayPluginWebSocketService>();
        services.AddSingleton<IOverlayPluginWebSocketSessionService, OverlayPluginWebSocketSessionService>();
        services.AddSingleton<OverlayPluginConnectionStateService>();
        services.AddSingleton<IOverlayPluginCurrentPlayerStateService, OverlayPluginCurrentPlayerStateService>();
        services.AddSingleton<ICharacterConfigDataService, CharacterConfigDataService>();
        services.AddSingleton<ICharacterProfileStore, CharacterProfileStore>();
        services.AddSingleton<ICraftHotbarRegistrationValidator, CraftHotbarRegistrationValidator>();
        services.AddSingleton<ICraftSequenceStore, CraftSequenceStore>();
        services.AddSingleton<CraftActionIconSourceService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IAutoCraftSequenceEditorDialogService, AutoCraftSequenceEditorDialogService>();
        services.AddSingleton<IAutoCraftActionExecutor, AutoCraftActionExecutor>();
        services.AddSingleton<OverlayWindowService>();
        services.AddSingleton<AutoCraftExecutionService>();

        services.AddSingleton<AppEventLogViewModel>();
        services.AddSingleton<OverlayWebSocketLogViewModel>();
        services.AddSingleton<DashboardStatusViewModel>();
        services.AddSingleton<DataSourceStatusViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<AutoCraftViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
