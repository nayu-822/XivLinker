using System.Text.Json;
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
    private readonly object syncRoot = new();
    private AppSettings current;

    public AppSettingsStore(IAppDataPathService appDataPathService)
    {
        this.appDataPathService = appDataPathService;
        current = LoadFromDiskCore();
    }

    public AppSettings Current
    {
        get
        {
            lock (syncRoot)
            {
                return current;
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
                return loadedSettings;
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
        catch (JsonException)
        {
            BackupBrokenFile(path);
            return new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
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

    private static void BackupBrokenFile(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        string backupPath = Path.Combine(
            directory,
            $"{fileNameWithoutExtension}.invalid-{DateTimeOffset.Now:yyyyMMddHHmmss}{extension}");
        File.Move(path, backupPath, overwrite: true);
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            FileLogLevel = settings.FileLogLevel,
        };
    }
}
