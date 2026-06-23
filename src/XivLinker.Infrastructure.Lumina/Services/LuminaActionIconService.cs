using Lumina;
using Lumina.Data.Files;
using System.Collections.Concurrent;
using System.IO;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaActionIconService
{
    private readonly ILuminaGameDataProvider gameDataProvider;
    private readonly ConcurrentDictionary<uint, Task<byte[]?>> iconCache = [];

    public LuminaActionIconService(ILuminaGameDataProvider gameDataProvider)
    {
        this.gameDataProvider = gameDataProvider;
    }

    public async Task<byte[]?> GetIconPngAsync(uint iconId, CancellationToken cancellationToken = default)
    {
        if (iconId == 0)
        {
            return null;
        }

        string cachePath = GetIconCachePath(iconId);
        if (File.Exists(cachePath))
        {
            return await File.ReadAllBytesAsync(cachePath, cancellationToken);
        }

        Task<byte[]?> loadTask = iconCache.GetOrAdd(
            iconId,
            static (key, state) => state.service.LoadAndCacheIconAsync(key, state.cachePath, state.cancellationToken),
            (service: this, cachePath, cancellationToken));

        try
        {
            return await loadTask;
        }
        catch (OperationCanceledException)
        {
            iconCache.TryRemove(iconId, out _);
            throw;
        }
        catch
        {
            iconCache.TryRemove(iconId, out _);
            return null;
        }
    }

    private async Task<byte[]?> LoadAndCacheIconAsync(
        uint iconId,
        string cachePath,
        CancellationToken cancellationToken)
    {
        byte[]? pngBytes = null;

        try
        {
            var gameData = await gameDataProvider.GetGameDataAsync(cancellationToken);
            if (gameData is null)
            {
                return null;
            }

            pngBytes = await Task.Run(() => LoadIconPng(gameData, iconId), cancellationToken);
            if (pngBytes is null)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllBytesAsync(cachePath, pngBytes, cancellationToken);
            return pngBytes;
        }
        finally
        {
            if (pngBytes is null)
            {
                iconCache.TryRemove(iconId, out _);
            }
        }
    }

    private static byte[]? LoadIconPng(GameData gameData, uint iconId)
    {
        TexFile? texture = gameData.GetFile<TexFile>(BuildIconPath(iconId, highResolution: true))
            ?? gameData.GetFile<TexFile>(BuildIconPath(iconId, highResolution: false));

        if (texture is null)
        {
            return null;
        }

        return LuminaTexturePngEncoder.Encode(texture);
    }

    private static string BuildIconPath(uint iconId, bool highResolution)
    {
        uint folder = iconId / 1000 * 1000;
        return highResolution
            ? $"ui/icon/{folder:D6}/{iconId:D6}_hr1.tex"
            : $"ui/icon/{folder:D6}/{iconId:D6}.tex";
    }

    private static string GetIconCachePath(uint iconId)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "XivLinker", "Cache", "Icons", $"{iconId:D6}.png");
    }
}
