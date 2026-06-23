using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XivLinker.Application.Abstractions;

namespace XivLinker.App.Services;

public sealed class CraftActionIconSourceService
{
    private readonly ICrafterActionCatalogService crafterActionCatalogService;
    private readonly ConcurrentDictionary<uint, ImageSource?> imageCache = [];

    public CraftActionIconSourceService(ICrafterActionCatalogService crafterActionCatalogService)
    {
        this.crafterActionCatalogService = crafterActionCatalogService;
    }

    public async Task<ImageSource?> GetIconSourceAsync(uint iconId, CancellationToken cancellationToken = default)
    {
        if (iconId == 0)
        {
            return null;
        }

        if (imageCache.TryGetValue(iconId, out ImageSource? cached))
        {
            return cached;
        }

        byte[]? pngBytes = await crafterActionCatalogService.GetIconPngAsync(iconId, cancellationToken);
        ImageSource? imageSource = CreateImageSource(pngBytes);
        imageCache[iconId] = imageSource;
        return imageSource;
    }

    private static ImageSource? CreateImageSource(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(pngBytes);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }
}
