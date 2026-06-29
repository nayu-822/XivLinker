#pragma warning disable CS0067
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XivLinker.App.Services;
using XivLinker.App.ViewModels;
using XivLinker.Application.Logging;
using XivLinker.Application.Services;
using XivLinker.Infrastructure.CharacterConfig.Models;
using XivLinker.Infrastructure.CharacterConfig.Services;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Models;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Constructor_SelectsInfoLevelByDefault()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = new(rootPath);

            Assert.Equal("INFO", context.ViewModel.SelectedFileLogLevel?.DisplayName);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task ChangingSelectedFileLogLevel_SavesSettings()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = new(rootPath);

            await InvokeSaveFileLogLevelAsync(context.ViewModel, XivLinkerLogLevel.Warn);

            await WaitUntilAsync(() =>
                context.SettingsStore.Current.FileLogLevel == XivLinkerLogLevel.Warn
                && File.Exists(context.PathService.SettingsFilePath));

            string json = await File.ReadAllTextAsync(context.PathService.SettingsFilePath);
            Assert.Contains("\"FileLogLevel\": 2", json);
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Fact]
    public async Task ChangingSelectedFileLogLevel_AddsEventLogMessage()
    {
        string rootPath = CreateAppDataRoot();

        try
        {
            using TestContext context = new(rootPath);

            await InvokeSaveFileLogLevelAsync(context.ViewModel, XivLinkerLogLevel.Error);

            await WaitUntilAsync(() =>
                context.EventLog.Items.Count > 1
                && context.EventLog.Items.Any(item => item.Message.Contains("ファイルログ出力レベル", StringComparison.Ordinal)));

            Assert.Contains(context.EventLog.Items, item => item.Message.Contains("ERROR", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(rootPath, true);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 50; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private static async Task InvokeSaveFileLogLevelAsync(SettingsViewModel viewModel, XivLinkerLogLevel level)
    {
        var method = typeof(SettingsViewModel).GetMethod("SaveFileLogLevelAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        object? result = method!.Invoke(viewModel, [level]);
        Task task = Assert.IsAssignableFrom<Task>(result);
        await task;
    }

    private static string CreateAppDataRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "XivLinkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(string rootPath)
        {
            PathService = new AppDataPathService(rootPath);
            SettingsStore = new AppSettingsStore(PathService);
            EventLog = new AppEventLogViewModel();

            var characterProfileStore = new FakeCharacterProfileStore();
            var gameDataService = new FakeGameDataService();
            var webSocketService = new FakeOverlayPluginWebSocketService();
            var sessionService = new FakeOverlayPluginWebSocketSessionService();
            var connectionStateService = new OverlayPluginConnectionStateService(
                webSocketService,
                sessionService,
                gameDataService,
                NullLogger<OverlayPluginConnectionStateService>.Instance);
            var dataSourceStatus = new DataSourceStatusViewModel(
                gameDataService,
                connectionStateService,
                characterProfileStore,
                EventLog,
                NullLogger<DataSourceStatusViewModel>.Instance);

            ViewModel = new SettingsViewModel(
                Options.Create(new OverlayPluginOptions()),
                Options.Create(new LuminaOptions()),
                dataSourceStatus,
                EventLog,
                new FakeFolderPickerService(),
                characterProfileStore,
                new FakeAppDataFolderService(PathService),
                new FakeConfirmationDialogService(),
                SettingsStore,
                NullLogger<SettingsViewModel>.Instance);
        }

        public AppDataPathService PathService { get; }

        public AppSettingsStore SettingsStore { get; }

        public AppEventLogViewModel EventLog { get; }

        public SettingsViewModel ViewModel { get; }

        public void Dispose()
        {
        }
    }

    private sealed class FakeAppDataFolderService : IAppDataFolderService
    {
        public FakeAppDataFolderService(AppDataPathService pathService)
        {
            AppDataRootPath = pathService.AppDataRootPath;
            CachePath = pathService.CacheRootPath;
            IconCachePath = pathService.IconCachePath;
            LogsPath = pathService.LogsPath;
        }

        public string AppDataRootPath { get; }

        public string CachePath { get; }

        public string IconCachePath { get; }

        public string LogsPath { get; }

        public Task OpenFolderAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AppDataFolderStats> GetStatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDataFolderStats(
                new AppDataStorageCategoryStats("Icon", IconCachePath, 0, 0, true),
                new AppDataStorageCategoryStats("Logs", LogsPath, 0, 0, true),
                new AppDataStorageCategoryStats("Cache", CachePath, 0, 0, true)));

        public Task DeleteIconCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteLogFilesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAllCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFolderPickerService : IFolderPickerService
    {
        public string? PickFolder(string? initialDirectory = null) => null;
    }

    private sealed class FakeConfirmationDialogService : IConfirmationDialogService
    {
        public bool Confirm(string title, string message) => true;
    }

    private sealed class FakeCharacterProfileStore : ICharacterProfileStore
    {
        public event EventHandler? StateChanged;

        public IReadOnlyList<CharacterProfile> Profiles { get; } = [];

        public CharacterProfile? SelectedProfile => null;

        public CharacterData? SelectedCharacterData => null;

        public string? SelectedCharacterDirectoryPath => null;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AddProfileAsync(string path, string? displayName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SelectProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReloadProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateDisplayNameAsync(string profileId, string? displayName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveProfileAsync(string profileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGameDataService : IGameDataService
    {
        public bool IsConfigured => true;

        public bool IsAvailable => true;

        public string? SqPackPath => "C:\\sqpack";

        public string? ErrorMessage => null;

        public Task<GameDataStatus> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameDataStatus
            {
                State = GameDataAvailabilityState.Ready,
                IsConfigured = true,
                IsAvailable = true,
                SqPackPath = SqPackPath,
            });
    }

    private sealed class FakeOverlayPluginWebSocketService : IOverlayPluginWebSocketService
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<JsonDocument> CallAsync(
            string call,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(JsonDocument.Parse("""{"version":"test"}"""));

        public Task<string?> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("test");
    }

    private sealed class FakeOverlayPluginWebSocketSessionService : IOverlayPluginWebSocketSessionService
    {
        public event EventHandler? ConnectionStateChanged;

        public event EventHandler<OverlayWebSocketCommunicationLogEntry>? CommunicationLogged;

        public event EventHandler<string>? EventReceived;

        public bool IsStarted { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            IsStarted = true;
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsStarted = false;
            ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task<string> SendRequestAsync(
            string call,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult("""{"version":"test"}""");

        public Task SendCommandAsync(
            string call,
            IReadOnlyDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SubscribeAsync(
            IEnumerable<string> events,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
#pragma warning restore CS0067
