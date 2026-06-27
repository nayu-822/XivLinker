using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using XivLinker.App.Services;
using XivLinker.Infrastructure.CharacterConfig.Models;
using XivLinker.Infrastructure.CharacterConfig.Services;
using XivLinker.Infrastructure.Lumina.Services;
using XivLinker.Infrastructure.Overlay.Services;

namespace XivLinker.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IFolderPickerService folderPickerService;
    private readonly ICharacterProfileStore characterProfileStore;

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

    public SettingsViewModel(
        IOptions<OverlayPluginOptions> overlayPluginOptions,
        IOptions<LuminaOptions> luminaOptions,
        DataSourceStatusViewModel dataSourceStatus,
        AppEventLogViewModel eventLog,
        IFolderPickerService folderPickerService,
        ICharacterProfileStore characterProfileStore)
    {
        this.folderPickerService = folderPickerService;
        this.characterProfileStore = characterProfileStore;

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
        CharacterProfiles = [];

        // These page view models are singletons today. If their lifetime changes,
        // move command-state updates into a shared log presenter/service.
        EventLog.Items.CollectionChanged += OnItemsChanged;
        characterProfileStore.StateChanged += OnCharacterProfileStoreStateChanged;
        RefreshCharacterProfiles();
    }

    public DataSourceStatusViewModel DataSourceStatus { get; }

    public AppEventLogViewModel EventLog { get; }

    public ObservableCollection<CharacterProfileItemViewModel> CharacterProfiles { get; }

    public string OverlayWebSocketUri { get; }

    public string SqPackPath { get; }

    public string CharacterConfigStatus => "登録済みキャラクター";

    public string CharacterConfigDescription => "FF14 のキャラクター設定フォルダーを登録して、対象キャラクターを切り替えられます。";

    public string LuminaPathDescription => "パスの変更や参照ダイアログは今後対応予定です。";

    public string LogRetentionDescription => "未実装: 現在はメモリ上のログをそのまま表示しています。";

    public IAsyncRelayCommand ReconnectOverlayCommand { get; }

    public IAsyncRelayCommand RefreshLuminaCommand { get; }

    public IRelayCommand ClearLogCommand { get; }

    public IAsyncRelayCommand AddCharacterProfileCommand { get; }

    public IAsyncRelayCommand<CharacterProfileItemViewModel> SelectCharacterProfileCommand { get; }

    public IAsyncRelayCommand<CharacterProfileItemViewModel> ReloadCharacterProfileCommand { get; }

    public IAsyncRelayCommand<CharacterProfileItemViewModel> RemoveCharacterProfileCommand { get; }

    public IAsyncRelayCommand SaveSelectedCharacterNameCommand { get; }

    partial void OnEditableSelectedCharacterNameChanged(string value)
    {
        SaveSelectedCharacterNameCommand.NotifyCanExecuteChanged();
    }

    private bool CanClearLog()
    {
        return EventLog.Items.Count > 0;
    }

    private void ClearLog()
    {
        EventLog.Clear();
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
        EventLog.Add($"キャラクター設定名を更新しました: {EditableSelectedCharacterName}");
    }

    private bool CanSaveSelectedCharacterName()
    {
        if (!HasSelectedCharacter)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(EditableSelectedCharacterName);
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
}
