using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IGameDataService gameDataService;
    private readonly OverlayPluginConnectionStateService overlayPluginConnectionStateService;
    private readonly ILogger<MainViewModel> logger;
    private readonly DataSourceStatusItemViewModel luminaItem;
    private readonly DataSourceStatusItemViewModel overlayItem;
    private readonly DashboardViewModel dashboardViewModel;
    private readonly AutoCraftViewModel autoCraftViewModel;

    [ObservableProperty]
    private string currentArea = "-";

    [ObservableProperty]
    private string currentCharacter = "-";

    [ObservableProperty]
    private string currentPageTitle = "\u30C0\u30C3\u30B7\u30E5\u30DC\u30FC\u30C9";

    [ObservableProperty]
    private object? currentContentViewModel;

    [ObservableProperty]
    private NavigationItemViewModel? selectedNavigationItem;

    public MainViewModel(
        IGameDataService gameDataService,
        OverlayPluginConnectionStateService overlayPluginConnectionStateService,
        ILogger<MainViewModel> logger)
    {
        this.gameDataService = gameDataService;
        this.overlayPluginConnectionStateService = overlayPluginConnectionStateService;
        this.logger = logger;

        RefreshLuminaCommand = new AsyncRelayCommand(RefreshGameDataStatusAsync, CanRefreshLumina);
        ConnectOverlayPluginCommand = new AsyncRelayCommand(ConnectOverlayPluginAsync, CanConnectOverlayPlugin);
        SelectNavigationItemCommand = new RelayCommand<NavigationItemViewModel>(SelectNavigationItem);

        luminaItem = new DataSourceStatusItemViewModel(
            "Lumina",
            "\u672A\u8A2D\u5B9A",
            "FF14 \u306E sqpack \u30D5\u30A9\u30EB\u30C0\u304C\u8A2D\u5B9A\u3055\u308C\u3066\u3044\u307E\u305B\u3093\u3002",
            "\u518D\u78BA\u8A8D",
            RefreshLuminaCommand);
        overlayItem = new DataSourceStatusItemViewModel(
            "OverlayPlugin WebSocket",
            "\u672A\u63A5\u7D9A",
            "ACT \u307E\u305F\u306F OverlayPlugin \u306E WebSocket \u30B5\u30FC\u30D0\u30FC\u306B\u63A5\u7D9A\u3057\u3066\u3044\u307E\u305B\u3093\u3002",
            "\u63A5\u7D9A",
            ConnectOverlayPluginCommand);

        DataSources = new ObservableCollection<DataSourceStatusItemViewModel>
        {
            overlayItem,
            luminaItem,
            new(
                "\u30AD\u30E3\u30E9\u30AF\u30BF\u30FC\u8A2D\u5B9A",
                "\u672A\u8A2D\u5B9A",
                "\u30AD\u30E3\u30E9\u30AF\u30BF\u30FC\u8A2D\u5B9A\u30D5\u30A1\u30A4\u30EB\u9023\u643A\u306F\u672A\u5B9F\u88C5\u3067\u3059\u3002"),
            new(
                "\u30ED\u30B0",
                "\u672A\u8A2D\u5B9A",
                "FF14 / ACT \u30ED\u30B0\u9023\u643A\u306F\u672A\u5B9F\u88C5\u3067\u3059\u3002"),
        };

        EventLogs = new ObservableCollection<string>
        {
            "XivLinker \u3092\u8D77\u52D5\u3057\u307E\u3057\u305F\u3002",
        };

        dashboardViewModel = new DashboardViewModel(this);
        autoCraftViewModel = new AutoCraftViewModel();

        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("dashboard", "\u30C0\u30C3\u30B7\u30E5\u30DC\u30FC\u30C9", "\u25A3", dashboardViewModel),
            new("auto-craft", "\u81EA\u52D5\u30AF\u30E9\u30D5\u30C8", "\u2692", autoCraftViewModel),
        };

        SelectedNavigationItem = NavigationItems[0];

        this.overlayPluginConnectionStateService.StateChanged += OnOverlayPluginStateChanged;
    }

    public ObservableCollection<DataSourceStatusItemViewModel> DataSources { get; }

    public ObservableCollection<string> EventLogs { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public IAsyncRelayCommand RefreshLuminaCommand { get; }

    public IAsyncRelayCommand ConnectOverlayPluginCommand { get; }

    public IRelayCommand<NavigationItemViewModel> SelectNavigationItemCommand { get; }

    public string ShellStatusText => overlayPluginConnectionStateService.State switch
    {
        OverlayPluginConnectionState.Connected => "\u63A5\u7D9A\u6E08\u307F",
        OverlayPluginConnectionState.Connecting => "\u63A5\u7D9A\u4E2D...",
        OverlayPluginConnectionState.Unavailable => "\u5229\u7528\u4E0D\u53EF",
        OverlayPluginConnectionState.Error => "\u30A8\u30E9\u30FC",
        _ => "\u672A\u63A5\u7D9A",
    };

    public async Task InitializeDataSourcesAsync()
    {
        await RefreshGameDataStatusAsync();
        await ConnectOverlayPluginAsync();
    }

    private async Task RefreshGameDataStatusAsync()
    {
        luminaItem.Status = "\u78BA\u8A8D\u4E2D...";
        luminaItem.Detail = "Lumina \u306E\u521D\u671F\u5316\u72B6\u614B\u3092\u78BA\u8A8D\u3057\u3066\u3044\u307E\u3059\u3002";

        try
        {
            GameDataStatus status = await gameDataService.CheckAvailabilityAsync();
            ApplyGameDataStatus(status);
            AppendLog($"Lumina \u72B6\u614B: {luminaItem.Status}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Lumina \u306E\u72B6\u614B\u78BA\u8A8D\u306B\u5931\u6557\u3057\u307E\u3057\u305F\u3002");
            luminaItem.Status = "\u30A8\u30E9\u30FC";
            luminaItem.Detail = "Lumina \u306E\u521D\u671F\u5316\u78BA\u8A8D\u3067\u30A8\u30E9\u30FC\u304C\u767A\u751F\u3057\u307E\u3057\u305F\u3002";
            AppendLog("Lumina \u306E\u521D\u671F\u5316\u78BA\u8A8D\u306B\u5931\u6557\u3057\u307E\u3057\u305F\u3002");
        }
    }

    private async Task ConnectOverlayPluginAsync()
    {
        overlayItem.Status = "\u63A5\u7D9A\u4E2D...";
        overlayItem.Detail = "OverlayPlugin WebSocket \u3078\u63A5\u7D9A\u3057\u3066\u3044\u307E\u3059\u3002";
        ConnectOverlayPluginCommand.NotifyCanExecuteChanged();

        try
        {
            await overlayPluginConnectionStateService.ConnectAsync();
            ApplyOverlayPluginStatus();
            AppendLog($"OverlayPlugin \u72B6\u614B: {overlayItem.Status}");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OverlayPlugin WebSocket \u3078\u306E\u63A5\u7D9A\u306B\u5931\u6557\u3057\u307E\u3057\u305F\u3002");
            ApplyOverlayPluginStatus();
            AppendLog("OverlayPlugin WebSocket \u3078\u306E\u63A5\u7D9A\u306B\u5931\u6557\u3057\u307E\u3057\u305F\u3002");
        }
    }

    private void ApplyGameDataStatus(GameDataStatus status)
    {
        switch (status.State)
        {
            case GameDataAvailabilityState.Unconfigured:
                luminaItem.Status = "\u672A\u8A2D\u5B9A";
                luminaItem.Detail = "FF14 \u306E sqpack \u30D5\u30A9\u30EB\u30C0\u304C\u8A2D\u5B9A\u3055\u308C\u3066\u3044\u307E\u305B\u3093\u3002";
                break;
            case GameDataAvailabilityState.PathNotFound:
                luminaItem.Status = "\u5229\u7528\u4E0D\u53EF";
                luminaItem.Detail = $"sqpack \u30D5\u30A9\u30EB\u30C0\u304C\u898B\u3064\u304B\u308A\u307E\u305B\u3093: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.Ready:
                luminaItem.Status = "\u5229\u7528\u53EF\u80FD";
                luminaItem.Detail = $"sqpack: {status.SqPackPath ?? "-"}";
                break;
            case GameDataAvailabilityState.InitializationFailed:
                luminaItem.Status = "\u30A8\u30E9\u30FC";
                luminaItem.Detail = status.ErrorMessage ?? "Lumina \u306E\u521D\u671F\u5316\u306B\u5931\u6557\u3057\u307E\u3057\u305F\u3002";
                break;
            default:
                luminaItem.Status = "\u4E0D\u660E";
                luminaItem.Detail = "Lumina \u306E\u72B6\u614B\u3092\u5224\u5B9A\u3067\u304D\u307E\u305B\u3093\u3067\u3057\u305F\u3002";
                break;
        }

        RefreshLuminaCommand.NotifyCanExecuteChanged();
    }

    private void ApplyOverlayPluginStatus()
    {
        overlayItem.Status = overlayPluginConnectionStateService.State switch
        {
            OverlayPluginConnectionState.Connecting => "\u63A5\u7D9A\u4E2D...",
            OverlayPluginConnectionState.Connected => "\u63A5\u7D9A\u6E08\u307F",
            OverlayPluginConnectionState.Unavailable => "\u5229\u7528\u4E0D\u53EF",
            OverlayPluginConnectionState.Error => "\u30A8\u30E9\u30FC",
            _ => "\u672A\u63A5\u7D9A",
        };

        overlayItem.Detail = overlayPluginConnectionStateService.Message;
        ConnectOverlayPluginCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShellStatusText));
    }

    private void OnOverlayPluginStateChanged(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            ApplyOverlayPluginStatus();
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(ApplyOverlayPluginStatus);
    }

    private void AppendLog(string message)
    {
        EventLogs.Insert(0, message);
    }

    private bool CanRefreshLumina()
    {
        return luminaItem.Status != "\u5229\u7528\u53EF\u80FD";
    }

    private bool CanConnectOverlayPlugin()
    {
        return overlayPluginConnectionStateService.State is not OverlayPluginConnectionState.Connected
            and not OverlayPluginConnectionState.Connecting;
    }

    partial void OnSelectedNavigationItemChanged(NavigationItemViewModel? value)
    {
        foreach (NavigationItemViewModel item in NavigationItems)
        {
            item.IsSelected = ReferenceEquals(item, value);
        }

        CurrentContentViewModel = value?.ContentViewModel;
        CurrentPageTitle = value?.Title ?? "XivLinker";
    }

    private void SelectNavigationItem(NavigationItemViewModel? item)
    {
        if (item is null || ReferenceEquals(item, SelectedNavigationItem))
        {
            return;
        }

        SelectedNavigationItem = item;
    }
}
