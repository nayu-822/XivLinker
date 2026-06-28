using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaGameDataService : IGameDataService, ILuminaGameDataProvider, IDisposable
{
    private static readonly string[] KnownSqPackPaths =
    [
        @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
        @"C:\Program Files\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
    ];

    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly LuminaOptions options;
    private readonly ILogger<LuminaGameDataService> logger;
    private GameData? gameData;
    private string? resolvedSqPackPath;

    public LuminaGameDataService(IOptions<LuminaOptions> options, ILogger<LuminaGameDataService> logger)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SqPackPath);

    public bool IsAvailable => gameData is not null;

    public string? SqPackPath => resolvedSqPackPath ??= ResolveSqPackPath();

    public string? ErrorMessage
    {
        get;
        private set;
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

            gameData = await Task.Run(() => new GameData(SqPackPath!), cancellationToken);
            ErrorMessage = null;
            return CreateStatus(GameDataAvailabilityState.Ready);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            gameData?.Dispose();
            gameData = null;
            ErrorMessage = exception.Message;
            return CreateStatus(GameDataAvailabilityState.InitializationFailed);
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public async Task<GameData?> GetGameDataAsync(CancellationToken cancellationToken = default)
    {
        GameDataStatus status = await CheckAvailabilityAsync(cancellationToken);
        return status.IsAvailable ? gameData : null;
    }

    public async Task<ResolvedMapLocation?> ResolveMapLocationAsync(
        uint? territoryTypeId,
        uint? mapId,
        float rawX,
        float rawY,
        float rawZ,
        CancellationToken cancellationToken = default)
    {
        GameData? data = await GetGameDataAsync(cancellationToken);
        if (data is null)
        {
            return null;
        }

        try
        {
            return await Task.Run(() => ResolveMapLocationCore(data, territoryTypeId, mapId, rawX, rawZ), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to resolve map location. TerritoryTypeId: {TerritoryTypeId}, MapId: {MapId}", territoryTypeId, mapId);
            return new ResolvedMapLocation
            {
                MapId = mapId ?? 0,
                MapName = "マップ情報を取得できません",
                CoordinatesText = "座標を変換できません",
                IssueMessage = "マップ情報の解決に失敗しました。",
            };
        }
    }

    public async Task<ResolvedClassJobInfo?> ResolveClassJobAsync(
        uint classJobId,
        int? level = null,
        CancellationToken cancellationToken = default)
    {
        GameData? data = await GetGameDataAsync(cancellationToken);
        if (data is null)
        {
            return null;
        }

        try
        {
            return await Task.Run(() => ResolveClassJobCore(data, classJobId, level), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to resolve class job. ClassJobId: {ClassJobId}", classJobId);
            return new ResolvedClassJobInfo
            {
                ClassJobId = classJobId,
                ClassJobName = $"Job ID: {classJobId}",
                Level = level,
                DisplayText = level is > 0 ? $"Job ID: {classJobId} Lv.{level}" : $"Job ID: {classJobId}",
                IssueMessage = "ジョブ情報の解決に失敗しました。",
            };
        }
    }

    public void Dispose()
    {
        initializationLock.Dispose();
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

    private static ResolvedMapLocation? ResolveMapLocationCore(GameData data, uint? territoryTypeId, uint? mapId, float rawX, float rawZ)
    {
        Map? map = null;
        string? mapName = null;

        if (territoryTypeId is not null and > 0)
        {
            var territorySheet = data.GetExcelSheet<TerritoryType>(Language.Japanese);
            if (territorySheet is not null)
            {
                TerritoryType territory = territorySheet.FirstOrDefault(row => row.RowId == territoryTypeId.Value);
                if (territory.RowId != 0)
                {
                    map = territory.Map.ValueNullable;
                    mapName = territory.PlaceNameZone.ValueNullable?.Name.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(mapName))
                    {
                        mapName = territory.PlaceName.ValueNullable?.Name.ToString().Trim();
                    }
                }
            }
        }

        if ((map is null || map.Value.RowId == 0) && mapId is not null and > 0)
        {
            var mapSheet = data.GetExcelSheet<Map>(Language.Japanese);
            if (mapSheet is not null)
            {
                Map mapRow = mapSheet.FirstOrDefault(row => row.RowId == mapId.Value);
                if (mapRow.RowId != 0)
                {
                    map = mapRow;
                    mapName ??= mapRow.PlaceName.ValueNullable?.Name.ToString().Trim();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(mapName))
        {
            mapName = map?.PlaceName.ValueNullable?.Name.ToString().Trim();
        }

        if (map is null)
        {
            return new ResolvedMapLocation
            {
                MapId = mapId ?? 0,
                MapName = string.IsNullOrWhiteSpace(mapName) ? $"Territory ID: {territoryTypeId ?? 0}" : mapName,
                CoordinatesText = "座標を変換できません",
                IssueMessage = territoryTypeId is not null and > 0
                    ? "TerritoryType に対応する Map が見つかりません。"
                    : "MapId に対応する Map が見つかりません。",
            };
        }

        double mapX = MapCoordinateCalculator.ConvertWorldToMapCoordinate(rawX, map.Value.OffsetX, map.Value.SizeFactor);
        double mapY = MapCoordinateCalculator.ConvertWorldToMapCoordinate(rawZ, map.Value.OffsetY, map.Value.SizeFactor);

        return new ResolvedMapLocation
        {
            MapId = map.Value.RowId,
            MapName = string.IsNullOrWhiteSpace(mapName) ? $"Territory ID: {territoryTypeId ?? 0}" : mapName,
            MapX = mapX,
            MapY = mapY,
            CoordinatesText = MapCoordinateCalculator.FormatCoordinates(mapX, mapY),
        };
    }

    private static ResolvedClassJobInfo? ResolveClassJobCore(GameData data, uint classJobId, int? level)
    {
        var classJobSheet = data.GetExcelSheet<ClassJob>(Language.Japanese);
        if (classJobSheet is null)
        {
            return null;
        }

        ClassJob classJob = classJobSheet.FirstOrDefault(row => row.RowId == classJobId);
        if (classJob.RowId == 0)
        {
            return null;
        }

        string jobName = classJob.Name.ToString().Trim();
        if (string.IsNullOrWhiteSpace(jobName))
        {
            jobName = $"Job ID: {classJobId}";
        }

        return new ResolvedClassJobInfo
        {
            ClassJobId = classJobId,
            ClassJobName = jobName,
            Level = level,
            DisplayText = level is > 0 ? $"{jobName} Lv.{level}" : jobName,
        };
    }
}
