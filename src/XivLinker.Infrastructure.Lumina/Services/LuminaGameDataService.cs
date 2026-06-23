using Lumina;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XivLinker.Application.Abstractions;
using XivLinker.Domain.Models.Crafting;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaGameDataService : IGameDataService, ICrafterActionCatalogService, IDisposable
{
    private static readonly string[] KnownSqPackPaths =
    [
        @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
        @"C:\Program Files\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
    ];

    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly SemaphoreSlim catalogLock = new(1, 1);
    private readonly ConcurrentDictionary<uint, Lazy<Task<byte[]?>>> iconCache = [];
    private readonly LuminaOptions options;
    private GameData? gameData;
    private IReadOnlyList<CraftActionDefinition>? crafterActions;
    private string? resolvedSqPackPath;

    public LuminaGameDataService(IOptions<LuminaOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SqPackPath);

    public bool IsAvailable => gameData is not null;

    public string? SqPackPath => resolvedSqPackPath ??= ResolveSqPackPath();

    public string? ErrorMessage
    {
        get; private set;
    }

    public async Task<GameDataStatus> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            ErrorMessage = null;
            return CreateStatus(GameDataAvailabilityState.Unconfigured);
        }

        if (!Directory.Exists(SqPackPath))
        {
            ErrorMessage = null;
            return CreateStatus(GameDataAvailabilityState.PathNotFound);
        }

        if (gameData is not null)
        {
            return CreateStatus(GameDataAvailabilityState.Ready);
        }

        await initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (gameData is not null)
            {
                return CreateStatus(GameDataAvailabilityState.Ready);
            }

            try
            {
                gameData = await Task.Run(() => new GameData(SqPackPath!), cancellationToken);
                ErrorMessage = null;
                return CreateStatus(GameDataAvailabilityState.Ready);
            }
            catch (Exception exception)
            {
                gameData?.Dispose();
                gameData = null;
                ErrorMessage = exception.Message;
                return CreateStatus(GameDataAvailabilityState.InitializationFailed);
            }
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public async Task<CrafterActionCatalogResult> GetCrafterActionsAsync(CancellationToken cancellationToken = default)
    {
        GameDataStatus status = await CheckAvailabilityAsync(cancellationToken);
        if (!status.IsAvailable || gameData is null)
        {
            return new CrafterActionCatalogResult([], status.ErrorMessage ?? "Lumina を初期化できませんでした。");
        }

        if (crafterActions is not null)
        {
            return new CrafterActionCatalogResult(crafterActions);
        }

        await catalogLock.WaitAsync(cancellationToken);

        try
        {
            if (crafterActions is not null)
            {
                return new CrafterActionCatalogResult(crafterActions);
            }

            IReadOnlyList<CraftActionDefinition> loadedActions = await Task.Run(
                () => LoadCrafterActions(gameData),
                cancellationToken);

            crafterActions = loadedActions;
            return new CrafterActionCatalogResult(loadedActions);
        }
        catch (Exception exception)
        {
            return new CrafterActionCatalogResult([], exception.Message);
        }
        finally
        {
            catalogLock.Release();
        }
    }

    public Task<byte[]?> GetIconPngAsync(uint iconId, CancellationToken cancellationToken = default)
    {
        if (iconId == 0)
        {
            return Task.FromResult<byte[]?>(null);
        }

        Lazy<Task<byte[]?>> lazyLoader = iconCache.GetOrAdd(
            iconId,
            static (key, state) => new Lazy<Task<byte[]?>>(
                () => state.service.LoadIconPngAsync(key, state.cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication),
            (service: this, cancellationToken));

        return lazyLoader.Value;
    }

    public void Dispose()
    {
        initializationLock.Dispose();
        catalogLock.Dispose();
        gameData?.Dispose();
    }

    private string? ResolveSqPackPath()
    {
        string? configuredPath = NormalizePath(options.SqPackPath);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        foreach (string candidate in KnownSqPackPaths)
        {
            string? normalizedCandidate = NormalizePath(candidate);
            if (!string.IsNullOrWhiteSpace(normalizedCandidate) && Directory.Exists(normalizedCandidate))
            {
                return normalizedCandidate;
            }
        }

        return configuredPath;
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string expandedPath = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.GetFullPath(expandedPath);
    }

    private GameDataStatus CreateStatus(GameDataAvailabilityState state)
    {
        return new GameDataStatus
        {
            State = state,
            IsConfigured = IsConfigured,
            IsAvailable = IsAvailable,
            SqPackPath = SqPackPath,
            ErrorMessage = ErrorMessage,
        };
    }

    private static IReadOnlyList<CraftActionDefinition> LoadCrafterActions(GameData data)
    {
        var sheet = data.GetExcelSheet<CraftAction>(Language.Japanese);
        if (sheet is null)
        {
            return [];
        }

        return sheet
            .Select(static row => CreateDefinition(row))
            .Where(static definition => definition is not null)
            .Cast<CraftActionDefinition>()
            .OrderBy(static definition => definition.Category, StringComparer.CurrentCulture)
            .ThenBy(static definition => definition.DisplayName, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static CraftActionDefinition? CreateDefinition(CraftAction row)
    {
        string displayName = row.Name.ToString().Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        uint rowId = Convert.ToUInt32(row.RowId);
        uint iconId = Convert.ToUInt32(row.Icon);

        return new CraftActionDefinition(
            CraftActionId.FromLuminaRowId(rowId),
            displayName,
            2500,
            "クラフターアクション",
            iconId);
    }

    private async Task<byte[]?> LoadIconPngAsync(uint iconId, CancellationToken cancellationToken)
    {
        try
        {
            string cachePath = GetIconCachePath(iconId);
            if (File.Exists(cachePath))
            {
                return await File.ReadAllBytesAsync(cachePath, cancellationToken);
            }

            GameDataStatus status = await CheckAvailabilityAsync(cancellationToken);
            if (!status.IsAvailable || gameData is null)
            {
                return null;
            }

            byte[]? pngBytes = await Task.Run(() => LoadIconPng(gameData, iconId), cancellationToken);
            if (pngBytes is null)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllBytesAsync(cachePath, pngBytes, cancellationToken);
            return pngBytes;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? LoadIconPng(GameData data, uint iconId)
    {
        TexFile? texture = data.GetFile<TexFile>(BuildIconPath(iconId, highResolution: true))
            ?? data.GetFile<TexFile>(BuildIconPath(iconId, highResolution: false));

        if (texture is null)
        {
            return null;
        }

        return EncodePng(texture);
    }

    private static byte[]? EncodePng(TexFile texture)
    {
        byte[] imageData = texture.ImageData;
        if (imageData.Length == 0)
        {
            return null;
        }

        int width = texture.Header.Width;
        int height = texture.Header.Height;
        int stride = width * 4;

        BitmapSource bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            imageData,
            stride);
        bitmap.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
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
