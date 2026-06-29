using XivLinker.Application.Settings;

namespace XivLinker.Application.Abstractions;

public interface IAppSettingsStore
{
    AppSettings Current { get; }

    event EventHandler? SettingsChanged;

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
