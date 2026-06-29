using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XivLinker.App.Services;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Logging;
using XivLinker.Application.Settings;
using XivLinker.Infrastructure.CharacterConfig.Models;
using XivLinker.Infrastructure.CharacterConfig.Services;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IFolderPickerService folderPickerService;
    private readonly ICharacterProfileStore characterProfileStore;
    private readonly IAppDataFolderService appDataFolderService;
    private readonly IConfirmationDialogService confirmationDialogService;
    private readonly IAppSettingsStore appSettingsStore;
    private readonly ILogger<SettingsViewModel> logger;
    private bool isUpdatingFileLogLevelSelection;

    [ObservableProperty]
    private string selectedCharacterName = "未選択";

    [ObservableProperty]
    private string editableSelectedCharacterName = string.Empty;

    [ObservableProperty]
    private string selectedCharacterStatus = "未設定";

    [ObservableProperty]
    private string selectedHotbarStatus = "未設定";

    [ObservableProperty]
    private string selectedKeybindStatus = "未設定";

    [ObservableProperty]
    private string selectedCharacterErrors = string.Empty;

    [ObservableProperty]
    private string selectedHotbarPath = "-";

    [ObservableProperty]
    private string selectedKeybindPath = "-";

    [ObservableProperty]
    private bool hasSelectedCharacter;

    [ObservableProperty]
    private string appDataRootPath = "-";

    [ObservableProperty]
    private string iconCacheSummary = "0 件 / 0 B";

    [ObservableProperty]
    private string logFilesSummary = "0 件 / 0 B";

    [ObservableProperty]
    private bool isAppDataOperationRunning;

    [ObservableProperty]
    private XivLinkerLogLevelOptionViewModel? selectedFileLogLevel;

    public SettingsViewModel(
        IOptions<OverlayPluginOptions> overlayPluginOptions,
        IOptions<LuminaOptions> luminaOptions,
        DataSourceStatusViewModel dataSourceStatus,
        AppEventLogViewModel eventLog,
        IFolderPickerService folderPickerService,
        ICharacterProfileStore characterProfileStore,
        IAppDataFolderService appDataFolderService,
        IConfirmationDialogService confirmationDialogService,
        IAppSettingsStore appSettingsStore,
        ILogger<SettingsViewModel> logger)
    {
        this.folderPickerService = folderPickerService;
        this.characterProfileStore = characterProfileStore;
        this.appDataFolderService = appDataFolderService;
        this.confirmationDialogService = confirmationDialogService;
        this.appSettingsStore = appSettingsStore;
        this.logger = logger;

        OverlayWebSocketUri = overlayPluginOptions.Value.WebSocketUri;
        SqPackPath = string.IsNullOrWhiteSpace(luminaOptions.Value.SqPackPath)
            ? "未設定"
            : luminaOptions.Value.SqPackPath!;
        DataSourceStatus = dataSourceStatus;
        EventLog = eventLog;
        ReconnectOverlayCommand = dataSourceStatus.ConnectOverlayPluginCommand;
        RefreshLuminaCommand = dataSourceStatus.RefreshLuminaCommand;
        ClearLogCommand = new RelayCommand(ClearLog, CanClearLog);
        AddCharacterProfileCommand = new AsyncRelayCommand(AddCharacterProfileAsync);
        SelectCharacterProfileCommand = new AsyncRelayCommand<CharacterProfileItemViewModel>(SelectCharacterProfileAsync);
        ReloadCharacterProfileCommand = new AsyncRelayCommand<CharacterProfileItemViewModel>(ReloadCharacterProfileAsync);
        RemoveCharacterProfileCommand = new AsyncRelayCommand<CharacterProfileItemViewModel>(RemoveCharacterProfileAsync);
        SaveSelectedCharacterNameCommand = new AsyncRelayCommand(SaveSelectedCharacterNameAsync, CanSaveSelectedCharacterName);
        OpenAppDataFolderCommand = new AsyncRelayCommand(OpenAppDataFolderAsync, CanRunAppDataOperation);
        OpenIconCacheFolderCommand = new AsyncRelayCommand(OpenIconCacheFolderAsync, CanRunAppDataOperation);
        OpenLogFolderCommand = new AsyncRelayCommand(OpenLogFolderAsync, CanRunAppDataOperation);
        ClearIconCacheCommand = new AsyncRelayCommand(ClearIconCacheAsync, CanRunAppDataOperation);
        ClearLogFilesCommand = new AsyncRelayCommand(ClearLogFilesAsync, CanRunAppDataOperation);
        RefreshAppDataStatsCommand = new AsyncRelayCommand(RefreshAppDataStatsAsync, CanRunAppDataOperation);
        CharacterProfiles = [];
        AppDataRootPath = appDataFolderService.AppDataRootPath;
        FileLogLevelOptions =
        [
            new XivLinkerLogLevelOptionViewModel(XivLinkerLogLevel.Debug, "DEBUG", "開発向けの詳細ログまでファイルへ出力します。"),
            new XivLinkerLogLevelOptionViewModel(XivLinkerLogLevel.Info, "INFO", "通常の動作確認に必要な主要ログを出力します。"),
            new XivLinkerLogLevelOptionViewModel(XivLinkerLogLevel.Warn, "WARN", "警告とエラーのみを出力します。"),
            new XivLinkerLogLevelOptionViewModel(XivLinkerLogLevel.Error, "ERROR", "エラーのみを出力します。"),
        ];
        selectedFileLogLevel = FindFileLogLevelOption(appSettingsStore.Current.FileLogLevel);

        EventLog.Items.CollectionChanged += OnItemsChanged;
        characterProfileStore.StateChanged += OnCharacterProfileStoreStateChanged;
        appSettingsStore.SettingsChanged += OnAppSettingsChanged;
        RefreshCharacterProfiles();
        _ = RefreshAppDataStatsAsync();
    }

    public DataSourceStatusViewModel DataSourceStatus { get; }

    public AppEventLogViewModel EventLog { get; }

    public ObservableCollection<CharacterProfileItemViewModel> CharacterProfiles { get; }

    public IReadOnlyList<XivLinkerLogLevelOptionViewModel> FileLogLevelOptions { get; }

    public string OverlayWebSocketUri { get; }

    public string SqPackPath { get; }

    public string CharacterConfigStatus => "利用中キャラクター設定";

    public string CharacterConfigDescription => "FF14 のキャラクター設定フォルダーを登録して、ホットバーやキーバインドの状態を確認できます。";

    public string CharacterNameDescription => "表示名は後から変更できます。変更内容はキャラクター設定フォルダー一覧に反映されます。";

    public string LuminaPathDescription => "ゲームデータの参照パスは初回導入時のみ設定が必要です。";

    public string LogRetentionDescription => "ファイルログは %LOCALAPPDATA%\\XivLinker\\Logs に日別で保存されます。出力レベルは DEBUG / INFO / WARN / ERROR から選択できます。";

    public IAsyncRelayCommand ReconnectOverlayCommand { get; }

    public IAsyncRelayCommand RefreshLuminaCommand { get; }

    public IRelayCommand ClearLogCommand { get; }

    public IAsyncRelayCommand AddCharacterProfileCommand { get; }

    public IAsyncRelayCommand<CharacterProfileItemViewModel> SelectCharacterProfileCommand { get; }

    public IAsyncRelayCommand<CharacterProfileItemViewModel> ReloadCharacterProfileCommand { get; }

    public IAsyncRelayCommand<CharacterProfileItemViewModel> RemoveCharacterProfileCommand { get; }

    public IAsyncRelayCommand SaveSelectedCharacterNameCommand { get; }

    public IAsyncRelayCommand OpenAppDataFolderCommand { get; }

    public IAsyncRelayCommand OpenIconCacheFolderCommand { get; }

    public IAsyncRelayCommand OpenLogFolderCommand { get; }

    public IAsyncRelayCommand ClearIconCacheCommand { get; }

    public IAsyncRelayCommand ClearLogFilesCommand { get; }

    public IAsyncRelayCommand RefreshAppDataStatsCommand { get; }

    partial void OnEditableSelectedCharacterNameChanged(string value)
    {
        SaveSelectedCharacterNameCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFileLogLevelChanged(XivLinkerLogLevelOptionViewModel? value)
    {
        if (value is null || isUpdatingFileLogLevelSelection)
        {
            return;
        }

        _ = SaveFileLogLevelAsync(value.Value);
    }

    partial void OnIsAppDataOperationRunningChanged(bool value)
    {
        OpenAppDataFolderCommand.NotifyCanExecuteChanged();
        OpenIconCacheFolderCommand.NotifyCanExecuteChanged();
        OpenLogFolderCommand.NotifyCanExecuteChanged();
        ClearIconCacheCommand.NotifyCanExecuteChanged();
        ClearLogFilesCommand.NotifyCanExecuteChanged();
        RefreshAppDataStatsCommand.NotifyCanExecuteChanged();
    }

    private bool CanClearLog()
    {
        return EventLog.Items.Count > 0;
    }

    private bool CanRunAppDataOperation()
    {
        return !IsAppDataOperationRunning;
    }

    private void ClearLog()
    {
        EventLog.Clear();
    }

    private Task OpenAppDataFolderAsync()
    {
        return appDataFolderService.OpenFolderAsync(appDataFolderService.AppDataRootPath);
    }

    private Task OpenIconCacheFolderAsync()
    {
        return appDataFolderService.OpenFolderAsync(appDataFolderService.IconCachePath);
    }

    private Task OpenLogFolderAsync()
    {
        return appDataFolderService.OpenFolderAsync(appDataFolderService.LogsPath);
    }

    private async Task ClearIconCacheAsync()
    {
        if (!confirmationDialogService.Confirm(
                "アイコンキャッシュを削除",
                "アイコンキャッシュを削除します。必要になったアイコンは次回表示時に再生成されます。削除しますか？"))
        {
            return;
        }

        IsAppDataOperationRunning = true;

        try
        {
            await appDataFolderService.DeleteIconCacheAsync();
            logger.LogInformation("アイコンキャッシュを削除しました。");
            EventLog.Add("アイコンキャッシュを削除しました。");
            await RefreshAppDataStatsAsync();
        }
        finally
        {
            IsAppDataOperationRunning = false;
        }
    }

    private async Task ClearLogFilesAsync()
    {
        if (!confirmationDialogService.Confirm(
                "ログファイルを削除",
                "保存済みのログファイルを削除します。現在表示中のアプリ内ログは削除されません。アプリが使用中のログファイルは削除できない場合があります。削除しますか？"))
        {
            return;
        }

        IsAppDataOperationRunning = true;

        try
        {
            await appDataFolderService.DeleteLogFilesAsync();
            logger.LogInformation("ログファイルを削除しました。");
            EventLog.Add("ログファイルを削除しました。");
            await RefreshAppDataStatsAsync();
        }
        finally
        {
            IsAppDataOperationRunning = false;
        }
    }

    private async Task RefreshAppDataStatsAsync()
    {
        AppDataFolderStats stats = await appDataFolderService.GetStatsAsync();
        AppDataRootPath = appDataFolderService.AppDataRootPath;
        IconCacheSummary = FormatSummary(stats.IconCache);
        LogFilesSummary = FormatSummary(stats.Logs);
    }

    private async Task SaveFileLogLevelAsync(XivLinkerLogLevel level)
    {
        AppSettings settings = appSettingsStore.Current;
        settings.FileLogLevel = level;

        await appSettingsStore.SaveAsync(settings);
        logger.LogInformation("ファイルログ出力レベルを変更しました。Level={FileLogLevel}", level.ToDisplayName());
        EventLog.Add($"ファイルログ出力レベルを {level.ToDisplayName()} に変更しました。");
    }

    private async Task AddCharacterProfileAsync()
    {
        string? selectedFolder = folderPickerService.PickFolder();
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        await characterProfileStore.AddProfileAsync(selectedFolder);
        AppendSelectedCharacterLoadLog("追加", selectedFolder);
    }

    private async Task SelectCharacterProfileAsync(CharacterProfileItemViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        await characterProfileStore.SelectProfileAsync(profile.Id);
        AppendSelectedCharacterLoadLog("選択", profile.DisplayName);
    }

    private async Task ReloadCharacterProfileAsync(CharacterProfileItemViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        await characterProfileStore.ReloadProfileAsync(profile.Id);
        AppendSelectedCharacterLoadLog("再読み込み", profile.DisplayName);
    }

    private async Task RemoveCharacterProfileAsync(CharacterProfileItemViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        await characterProfileStore.RemoveProfileAsync(profile.Id);
        EventLog.Add($"キャラクター設定を削除しました: {profile.DisplayName}", "Warning");
    }

    private async Task SaveSelectedCharacterNameAsync()
    {
        CharacterProfile? selectedProfile = characterProfileStore.SelectedProfile;
        if (selectedProfile is null)
        {
            return;
        }

        await characterProfileStore.UpdateDisplayNameAsync(selectedProfile.Id, EditableSelectedCharacterName);
        string savedName = characterProfileStore.SelectedProfile?.DisplayName ?? EditableSelectedCharacterName;
        EventLog.Add($"キャラクター表示名を更新しました: {savedName}");
    }

    private bool CanSaveSelectedCharacterName()
    {
        return HasSelectedCharacter;
    }

    private void RefreshCharacterProfiles()
    {
        CharacterProfiles.Clear();

        foreach (CharacterProfile profile in characterProfileStore.Profiles)
        {
            CharacterProfiles.Add(new CharacterProfileItemViewModel
            {
                Id = profile.Id,
                DisplayName = profile.DisplayName,
                CharacterSettingsDirectory = profile.CharacterSettingsDirectory,
                HotbarDatPath = profile.HotbarDatPath ?? Path.Combine(profile.CharacterSettingsDirectory, "HOTBAR.DAT"),
                KeybindDatPath = profile.KeybindDatPath ?? Path.Combine(profile.CharacterSettingsDirectory, "KEYBIND.DAT"),
                IsSelected = profile.IsSelected,
                LastLoadedAtText = profile.LastLoadedAt?.ToString("yyyy/MM/dd HH:mm:ss") ?? "未読み込み",
            });
        }

        CharacterProfile? selectedProfile = characterProfileStore.SelectedProfile;
        CharacterData? selectedData = characterProfileStore.SelectedCharacterData;

        HasSelectedCharacter = selectedProfile is not null;
        SelectedCharacterName = selectedProfile?.DisplayName ?? "未選択";
        EditableSelectedCharacterName = selectedProfile?.DisplayName ?? string.Empty;
        SelectedHotbarPath = selectedData?.HotbarAnalysisResult.FilePath
            ?? selectedProfile?.HotbarDatPath
            ?? (selectedProfile is null ? "-" : Path.Combine(selectedProfile.CharacterSettingsDirectory, "HOTBAR.DAT"));
        SelectedKeybindPath = selectedData?.KeybindAnalysisResult.FilePath
            ?? selectedProfile?.KeybindDatPath
            ?? (selectedProfile is null ? "-" : Path.Combine(selectedProfile.CharacterSettingsDirectory, "KEYBIND.DAT"));

        if (selectedProfile is null)
        {
            SelectedCharacterStatus = "未設定";
            SelectedHotbarStatus = "未設定";
            SelectedKeybindStatus = "未設定";
            SelectedCharacterErrors = string.Empty;
            SaveSelectedCharacterNameCommand.NotifyCanExecuteChanged();
            return;
        }

        if (selectedData is null)
        {
            SelectedCharacterStatus = "未読み込み";
            SelectedHotbarStatus = "未確認";
            SelectedKeybindStatus = "未確認";
            SelectedCharacterErrors = string.Empty;
            SaveSelectedCharacterNameCommand.NotifyCanExecuteChanged();
            return;
        }

        SelectedHotbarStatus = selectedData.HotbarAnalysisResult.Exists ? "読み込み済み" : "利用不可";
        SelectedKeybindStatus = selectedData.KeybindAnalysisResult.Exists ? "読み込み済み" : "利用不可";
        SelectedCharacterStatus = selectedData.Errors.Count == 0 ? "読み込み済み" : "エラー";
        SelectedCharacterErrors = selectedData.Errors.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, selectedData.Errors);
        SaveSelectedCharacterNameCommand.NotifyCanExecuteChanged();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearLogCommand.NotifyCanExecuteChanged();
    }

    private void OnCharacterProfileStoreStateChanged(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            RefreshCharacterProfiles();
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshCharacterProfiles);
    }

    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        void Apply()
        {
            isUpdatingFileLogLevelSelection = true;
            try
            {
                SelectedFileLogLevel = FindFileLogLevelOption(appSettingsStore.Current.FileLogLevel);
            }
            finally
            {
                isUpdatingFileLogLevelSelection = false;
            }
        }

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() != false)
        {
            Apply();
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(Apply);
    }

    private void AppendSelectedCharacterLoadLog(string action, string profileName)
    {
        CharacterData? selectedData = characterProfileStore.SelectedCharacterData;
        if (selectedData is null)
        {
            EventLog.Add($"キャラクター設定を{action}しました: {profileName}");
            return;
        }

        if (selectedData.Errors.Count == 0)
        {
            EventLog.Add($"キャラクター設定を{action}し、HOTBAR.DAT と KEYBIND.DAT を読み込みました: {selectedData.Profile.DisplayName}");
            return;
        }

        EventLog.Add(
            $"キャラクター設定の{action}後に読み込みエラーが発生しました: {selectedData.Profile.DisplayName} / {string.Join(" | ", selectedData.Errors)}",
            "Error");
    }

    private static string FormatSummary(AppDataStorageCategoryStats stats)
    {
        return $"{stats.FileCount} 件 / {FormatBytes(stats.TotalBytes)}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }

    private XivLinkerLogLevelOptionViewModel FindFileLogLevelOption(XivLinkerLogLevel level)
    {
        return FileLogLevelOptions.FirstOrDefault(option => option.Value == level)
            ?? FileLogLevelOptions.First(option => option.Value == XivLinkerLogLevel.Info);
    }
}
