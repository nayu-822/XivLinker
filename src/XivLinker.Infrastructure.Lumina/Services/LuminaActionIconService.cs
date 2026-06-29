using Lumina;
using Lumina.Data.Files;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XivLinker.Application.Abstractions;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaActionIconService
{
    private readonly ILuminaGameDataProvider gameDataProvider;
    private readonly IAppDataPathService appDataPathService;
    private readonly ILogger<LuminaActionIconService> logger;
    private readonly ConcurrentDictionary<uint, Task<byte[]?>> iconCache = [];

    public LuminaActionIconService(
        ILuminaGameDataProvider gameDataProvider,
        IAppDataPathService appDataPathService,
        ILogger<LuminaActionIconService>? logger = null)
    {
        this.gameDataProvider = gameDataProvider;
        this.appDataPathService = appDataPathService;
        this.logger = logger ?? NullLogger<LuminaActionIconService>.Instance;
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
            logger.LogDebug("アクションアイコンをキャッシュから読み込みます。IconId={IconId}, Path={Path}", iconId, cachePath);
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
        catch (Exception exception)
        {
            iconCache.TryRemove(iconId, out _);
            logger.LogError(exception, "アクションアイコンの取得に失敗しました。IconId={IconId}", iconId);
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
                logger.LogWarning("Lumina のゲームデータが利用できないためアクションアイコンを取得できません。IconId={IconId}", iconId);
                return null;
            }

            pngBytes = await Task.Run(() => LoadIconPng(gameData, iconId), cancellationToken);
            if (pngBytes is null)
            {
                logger.LogWarning("Lumina からアクションアイコンを取得できませんでした。IconId={IconId}", iconId);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllBytesAsync(cachePath, pngBytes, cancellationToken);
            logger.LogDebug("アクションアイコンをキャッシュへ保存しました。IconId={IconId}, Path={Path}", iconId, cachePath);
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

    private string GetIconCachePath(uint iconId)
    {
        return Path.Combine(appDataPathService.IconCachePath, $"{iconId:D6}.png");
    }
}
