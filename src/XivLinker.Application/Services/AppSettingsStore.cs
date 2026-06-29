using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Application.Abstractions;
using XivLinker.Application.Settings;

namespace XivLinker.Application.Services;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IAppDataPathService appDataPathService;
    private readonly ILogger<AppSettingsStore> logger;
    private readonly object syncRoot = new();
    private AppSettings current;

    public AppSettingsStore(
        IAppDataPathService appDataPathService,
        ILogger<AppSettingsStore>? logger = null)
    {
        this.appDataPathService = appDataPathService;
        this.logger = logger ?? NullLogger<AppSettingsStore>.Instance;
        current = LoadFromDiskCore();
    }

    public AppSettings Current
    {
        get
        {
            lock (syncRoot)
            {
                return Clone(current);
            }
        }
    }

    public event EventHandler? SettingsChanged;

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppSettings loadedSettings = LoadFromDiskCore();

                lock (syncRoot)
                {
                    current = loadedSettings;
                }

                SettingsChanged?.Invoke(this, EventArgs.Empty);
                return Clone(loadedSettings);
            },
            cancellationToken);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppSettings normalized = Clone(settings);
                WriteAtomically(
                    appDataPathService.SettingsFilePath,
                    JsonSerializer.Serialize(normalized, JsonOptions));

                lock (syncRoot)
                {
                    current = normalized;
                }

                SettingsChanged?.Invoke(this, EventArgs.Empty);
                logger.LogInformation("アプリ設定ファイルを保存しました。Path={Path}", appDataPathService.SettingsFilePath);
            },
            cancellationToken);
    }

    private AppSettings LoadFromDiskCore()
    {
        string path = appDataPathService.SettingsFilePath;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(path);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch (JsonException exception)
        {
            string backupPath = BackupBrokenFile(path);
            logger.LogWarning(
                exception,
                "アプリ設定ファイルの JSON 形式が不正だったため退避しました。Path={Path}, BackupPath={BackupPath}",
                path,
                backupPath);
            return new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "アプリ設定ファイルを読み込めなかったため既定値を使用します。Path={Path}", path);
            return new AppSettings();
        }
    }

    private static void WriteAtomically(string path, string content)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tempPath, content);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
            return;
        }

        File.Move(tempPath, path);
    }

    private static string BackupBrokenFile(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string backupPath = Path.Combine(
            directory,
            $"{fileNameWithoutExtension}.invalid-{DateTimeOffset.Now:yyyyMMddHHmmss}{extension}");
        File.Move(path, backupPath, overwrite: true);
        return backupPath;
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            FileLogLevel = settings.FileLogLevel,
        };
    }
}
