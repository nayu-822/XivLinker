using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LuminaGameDataOptions = Lumina.LuminaOptions;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaGameDataService : IGameDataService, ILuminaGameDataProvider, IDisposable
{
    private static readonly ConcurrentDictionary<Type, MethodInfo?> ExtractTextMethodCache = new();
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

            gameData = await Task.Run(
                () => new GameData(
                    SqPackPath!,
                    new LuminaGameDataOptions
                    {
                        LoadMultithreaded = false,
                    }),
                cancellationToken);
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
            logger.LogDebug(
                "Resolving map location. TerritoryTypeId: {TerritoryTypeId}, CurrentMapID: {MapId}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}",
                territoryTypeId,
                mapId,
                rawX,
                rawY,
                rawZ);

            ResolvedMapLocation? location = await Task.Run(
                () => ResolveMapLocationCore(data, territoryTypeId, mapId, rawX, rawZ),
                cancellationToken);

            if (location is null)
            {
                logger.LogWarning(
                    "Map resolve returned null. TerritoryTypeId: {TerritoryTypeId}, CurrentMapID: {MapId}, RawX: {RawX}, RawZ: {RawZ}",
                    territoryTypeId,
                    mapId,
                    rawX,
                    rawZ);
                return null;
            }

            if (string.IsNullOrWhiteSpace(location.IssueMessage))
            {
                logger.LogDebug(
                    "Map resolved. Source: {Source}, TerritoryTypeFound: {TerritoryTypeFound}, TerritoryType.Map found: {TerritoryMapFound}, Map.RowId: {MapRowId}, MapName: {MapName}, OffsetX: {OffsetX}, OffsetY: {OffsetY}, SizeFactor: {SizeFactor}",
                    location.ResolutionSource,
                    location.TerritoryTypeFound,
                    location.TerritoryMapFound,
                    location.MapId,
                    location.MapName,
                    location.OffsetX,
                    location.OffsetY,
                    location.SizeFactor);
            }
            else
            {
                logger.LogWarning(
                    "Map resolve failed. TerritoryTypeId: {TerritoryTypeId}, CurrentMapID: {MapId}, RawX: {RawX}, RawZ: {RawZ}, TerritoryTypeFound: {TerritoryTypeFound}, TerritoryType.Map found: {TerritoryMapFound}, Issue: {IssueMessage}",
                    territoryTypeId,
                    mapId,
                    rawX,
                    rawZ,
                    location.TerritoryTypeFound,
                    location.TerritoryMapFound,
                    location.IssueMessage);
            }

            return location;
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
        string? resolutionSource = null;
        bool territoryTypeFound = false;
        bool territoryMapFound = false;

        if (territoryTypeId is not null and > 0)
        {
            var territorySheet = data.GetExcelSheet<TerritoryType>(Language.Japanese);
            if (territorySheet is not null)
            {
                TerritoryType territory = territorySheet.FirstOrDefault(row => row.RowId == territoryTypeId.Value);
                if (territory.RowId != 0)
                {
                    territoryTypeFound = true;
                    map = territory.Map.ValueNullable;
                    territoryMapFound = map is not null && map.Value.RowId != 0;
                    resolutionSource = territoryMapFound ? "TerritoryType.Map" : null;
                    mapName = ExtractPlaceName(territory.PlaceNameZone.ValueNullable);
                    if (string.IsNullOrWhiteSpace(mapName))
                    {
                        mapName = ExtractPlaceName(territory.PlaceName.ValueNullable);
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
                    resolutionSource ??= "CurrentMapID fallback";
                    mapName ??= ExtractPlaceName(mapRow.PlaceName.ValueNullable);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(mapName))
        {
            mapName = map is not null ? ExtractPlaceName(map.Value.PlaceName.ValueNullable) : null;
        }

        if (map is null)
        {
            return new ResolvedMapLocation
            {
                MapId = mapId ?? 0,
                MapName = string.IsNullOrWhiteSpace(mapName) ? $"Territory ID: {territoryTypeId ?? 0}" : mapName,
                ResolutionSource = resolutionSource,
                TerritoryTypeFound = territoryTypeFound,
                TerritoryMapFound = territoryMapFound,
                CoordinatesText = "座標を変換できません",
                IssueMessage = $"Lumina の Map を解決できませんでした。TerritoryTypeId={territoryTypeId}, CurrentMapID={mapId}",
            };
        }

        return CreateResolvedMapLocation(
            map.Value,
            territoryTypeId,
            mapName,
            rawX,
            rawZ,
            resolutionSource,
            territoryTypeFound,
            territoryMapFound);
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

        string jobName = ExtractTextSafely(classJob.Name) ?? string.Empty;
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

    private static ResolvedMapLocation CreateResolvedMapLocation(
        Map map,
        uint? territoryTypeId,
        string? mapName,
        float rawX,
        float rawZ,
        string? resolutionSource,
        bool territoryTypeFound,
        bool territoryMapFound)
    {
        double mapX = MapCoordinateCalculator.ConvertWorldToMapCoordinate(rawX, map.OffsetX, map.SizeFactor);
        double mapY = MapCoordinateCalculator.ConvertWorldToMapCoordinate(rawZ, map.OffsetY, map.SizeFactor);

        return new ResolvedMapLocation
        {
            MapId = map.RowId,
            MapName = string.IsNullOrWhiteSpace(mapName) ? $"Territory ID: {territoryTypeId ?? 0}" : mapName,
            ResolutionSource = resolutionSource,
            TerritoryTypeFound = territoryTypeFound,
            TerritoryMapFound = territoryMapFound,
            OffsetX = map.OffsetX,
            OffsetY = map.OffsetY,
            SizeFactor = map.SizeFactor,
            MapX = mapX,
            MapY = mapY,
            CoordinatesText = MapCoordinateCalculator.FormatCoordinates(mapX, mapY),
        };
    }

    private static string? ExtractPlaceName(PlaceName? placeName)
    {
        return placeName is null ? null : ExtractTextSafely(placeName.Value.Name);
    }

    private static string? ExtractTextSafely<T>(T? value)
    {
        if (value is null)
        {
            return null;
        }

        object boxedValue = value;
        Type type = boxedValue.GetType();
        MethodInfo? extractTextMethod = ExtractTextMethodCache.GetOrAdd(type, static currentType =>
        {
            MethodInfo? instanceMethod = currentType.GetMethod(
                "ExtractText",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);
            if (instanceMethod is not null && instanceMethod.ReturnType == typeof(string))
            {
                return instanceMethod;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(static x => x is not null).Cast<Type>().ToArray();
                }

                foreach (Type candidateType in types)
                {
                    if (!candidateType.IsSealed || !candidateType.IsAbstract)
                    {
                        continue;
                    }

                    foreach (MethodInfo method in candidateType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (!string.Equals(method.Name, "ExtractText", StringComparison.Ordinal)
                            || method.ReturnType != typeof(string))
                        {
                            continue;
                        }

                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(currentType))
                        {
                            return method;
                        }
                    }
                }
            }

            return null;
        });

        string? text = extractTextMethod switch
        {
            not null when extractTextMethod.IsStatic => extractTextMethod.Invoke(null, [boxedValue]) as string,
            not null => extractTextMethod.Invoke(boxedValue, null) as string,
            _ => boxedValue.ToString(),
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
