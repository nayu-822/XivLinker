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
        string? mapName,
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
                "Resolving map location. TerritoryTypeId: {TerritoryTypeId}, CurrentMapID: {MapId}, MapName: {MapName}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}",
                territoryTypeId,
                mapId,
                mapName,
                rawX,
                rawY,
                rawZ);

            logger.LogInformation(
                "Map coordinate resolution detail: {ResolutionDetail}",
                DescribeMapCoordinateResolution(data, mapId, territoryTypeId, mapName, rawX, rawY, Language.Japanese));

            ResolvedMapLocation? location = await Task.Run(
                () => ResolveMapLocationCore(data, territoryTypeId, mapId, mapName, rawX, rawY),
                cancellationToken);

            if (location is null)
            {
                logger.LogWarning(
                    "Map resolve returned null. TerritoryTypeId: {TerritoryTypeId}, CurrentMapID: {MapId}, RawX: {RawX}, RawY: {RawY}",
                    territoryTypeId,
                    mapId,
                    rawX,
                    rawY);
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
                    "Map resolve failed. TerritoryTypeId: {TerritoryTypeId}, CurrentMapID: {MapId}, RawX: {RawX}, RawY: {RawY}, TerritoryTypeFound: {TerritoryTypeFound}, TerritoryType.Map found: {TerritoryMapFound}, Issue: {IssueMessage}",
                    territoryTypeId,
                    mapId,
                    rawX,
                    rawY,
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

    private static ResolvedMapLocation? ResolveMapLocationCore(
        GameData data,
        uint? territoryTypeId,
        uint? mapId,
        string? requestedMapName,
        float rawX,
        float rawY)
    {
        MapResolutionResult resolution = ResolveMapRow(
            data,
            mapId,
            territoryTypeId,
            requestedMapName,
            Language.Japanese);

        string? resolvedMapName = resolution.MapName ?? requestedMapName;

        if (resolution.Map is null)
        {
            return new ResolvedMapLocation
            {
                MapId = mapId ?? 0,
                MapName = string.IsNullOrWhiteSpace(resolvedMapName) ? $"Territory ID: {territoryTypeId ?? 0}" : resolvedMapName,
                ResolutionSource = resolution.Source,
                TerritoryTypeFound = resolution.TerritoryTypeFound,
                TerritoryMapFound = resolution.TerritoryMapFound,
                CoordinatesText = "蠎ｧ讓吶ｒ螟画鋤縺ｧ縺阪∪縺帙ｓ",
                IssueMessage = resolution.Issue ?? $"Lumina 縺ｮ Map 繧定ｧ｣豎ｺ縺ｧ縺阪∪縺帙ｓ縺ｧ縺励◆縲５erritoryTypeId={territoryTypeId}, CurrentMapID={mapId}",
            };
        }

        return CreateResolvedMapLocation(
            resolution.Map.Value,
            territoryTypeId,
            resolvedMapName,
            rawX,
            rawY,
            resolution.Source,
            resolution.TerritoryTypeFound,
            resolution.TerritoryMapFound);

        /*
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
            rawY,
            resolutionSource,
            territoryTypeFound,
            territoryMapFound);
        */
    }

    private static MapResolutionResult ResolveMapRow(
        GameData data,
        uint? mapId,
        uint? territoryTypeId,
        string? mapName,
        Language language)
    {
        MapResolutionCandidate? territoryCandidate = ResolveMapRowFromTerritoryType(data, territoryTypeId, language);
        bool territoryTypeFound = territoryCandidate is not null;
        bool territoryMapFound = territoryCandidate?.Map is Map territoryMap && territoryMap.RowId != 0;

        if (territoryCandidate?.Map is Map resolvedTerritoryMap && IsUsableMapForCoordinates(resolvedTerritoryMap))
        {
            return MapResolutionResult.Success(
                resolvedTerritoryMap,
                territoryCandidate.Source,
                territoryCandidate.TerritoryTypeId,
                territoryCandidate.MapName,
                territoryTypeFound: true,
                territoryMapFound: true);
        }

        MapResolutionCandidate? mapIdCandidate = ResolveMapRowFromMapId(data, mapId, language);
        if (mapIdCandidate?.Map is Map resolvedMapIdMap && IsUsableMapForCoordinates(resolvedMapIdMap))
        {
            return MapResolutionResult.Success(
                resolvedMapIdMap,
                mapIdCandidate.Source,
                territoryCandidate?.TerritoryTypeId,
                mapIdCandidate.MapName ?? territoryCandidate?.MapName,
                territoryTypeFound,
                territoryMapFound);
        }

        MapResolutionCandidate? mapNameCandidate = ResolveMapRowByMapName(data, mapName, language);
        if (mapNameCandidate?.Map is Map resolvedMapNameMap && IsUsableMapForCoordinates(resolvedMapNameMap))
        {
            return MapResolutionResult.Success(
                resolvedMapNameMap,
                mapNameCandidate.Source,
                mapNameCandidate.TerritoryTypeId ?? territoryCandidate?.TerritoryTypeId,
                mapNameCandidate.MapName,
                territoryTypeFound,
                territoryMapFound);
        }

        return MapResolutionResult.Failed(
            territoryCandidate,
            mapIdCandidate,
            mapNameCandidate,
            territoryTypeFound,
            territoryMapFound);
    }

    private static MapResolutionCandidate? ResolveMapRowFromTerritoryType(GameData data, uint? territoryTypeId, Language language)
    {
        if (territoryTypeId is null or 0)
        {
            return null;
        }

        var territorySheet = data.GetExcelSheet<TerritoryType>(language);
        if (territorySheet is null)
        {
            return null;
        }

        TerritoryType? territory = null;
        foreach (TerritoryType row in territorySheet)
        {
            if (row.RowId == territoryTypeId.Value)
            {
                territory = row;
                break;
            }
        }

        if (territory is null || territory.Value.RowId == 0)
        {
            return null;
        }

        string? placeName = ExtractPlaceName(territory.Value.PlaceNameZone.ValueNullable)
            ?? ExtractPlaceName(territory.Value.PlaceName.ValueNullable);

        return new MapResolutionCandidate
        {
            Source = "territoryType.Map",
            Map = territory.Value.Map.ValueNullable,
            TerritoryTypeId = territory.Value.RowId,
            MapName = placeName,
        };
    }

    private static MapResolutionCandidate? ResolveMapRowFromMapId(GameData data, uint? mapId, Language language)
    {
        if (mapId is null or 0)
        {
            return null;
        }

        var mapSheet = data.GetExcelSheet<Map>(language);
        if (mapSheet is null)
        {
            return null;
        }

        Map? map = null;
        foreach (Map row in mapSheet)
        {
            if (row.RowId == mapId.Value)
            {
                map = row;
                break;
            }
        }

        if (map is null || map.Value.RowId == 0)
        {
            return null;
        }

        return new MapResolutionCandidate
        {
            Source = "mapId",
            Map = map.Value,
            MapName = ExtractPlaceName(map.Value.PlaceName.ValueNullable),
        };
    }

    private static MapResolutionCandidate? ResolveMapRowByMapName(GameData data, string? mapName, Language language)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return null;
        }

        string normalizedTargetName = NormalizeComparisonText(mapName);
        var territorySheet = data.GetExcelSheet<TerritoryType>(language);
        if (territorySheet is null)
        {
            return null;
        }

        foreach (TerritoryType territoryType in territorySheet)
        {
            string? placeName = ExtractPlaceName(territoryType.PlaceName.ValueNullable);
            if (!string.Equals(NormalizeComparisonText(placeName), normalizedTargetName, StringComparison.Ordinal))
            {
                continue;
            }

            Map? map = territoryType.Map.ValueNullable;
            if (map is not null && map.Value.RowId != 0)
            {
                return new MapResolutionCandidate
                {
                    Source = "mapName",
                    Map = map.Value,
                    TerritoryTypeId = territoryType.RowId,
                    MapName = placeName,
                };
            }
        }

        return null;
    }

    private static bool IsUsableMapForCoordinates(Map map)
    {
        return map.RowId != 0 && map.SizeFactor > 0;
    }

    private static string NormalizeComparisonText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    private static string DescribeMapCoordinateResolution(
        GameData data,
        uint? mapId,
        uint? territoryTypeId,
        string? mapName,
        double posX,
        double posY,
        Language language)
    {
        string territoryMapDescription = DescribeResolvedMapRow(
            "territoryType.Map",
            ResolveMapRowFromTerritoryType(data, territoryTypeId, language)?.Map,
            posX,
            posY);
        string directMapDescription = DescribeResolvedMapRow(
            "mapId",
            ResolveMapRowFromMapId(data, mapId, language)?.Map,
            posX,
            posY);
        string nameMapDescription = DescribeResolvedMapRow(
            "mapName",
            ResolveMapRowByMapName(data, mapName, language)?.Map,
            posX,
            posY);

        MapResolutionResult final = ResolveMapRow(data, mapId, territoryTypeId, mapName, language);
        string finalDescription = DescribeResolvedMapRow("final", final.Map, posX, posY);

        return $"input(mapId={mapId?.ToString() ?? "-"}, territoryTypeId={territoryTypeId?.ToString() ?? "-"}, mapName={mapName ?? "-"}) | {territoryMapDescription} | {directMapDescription} | {nameMapDescription} | {finalDescription}";
    }

    private static string DescribeResolvedMapRow(string source, Map? map, double posX, double posY)
    {
        if (map is null)
        {
            return $"{source}=null";
        }

        string? placeName = ExtractPlaceName(map.Value.PlaceName.ValueNullable);
        if (map.Value.SizeFactor <= 0)
        {
            return $"{source}=row:{map.Value.RowId} place:{placeName ?? "-"} size:{map.Value.SizeFactor} invalid-coordinates";
        }

        double standardX = MapCoordinateCalculator.ConvertWorldToMapCoordinate(posX, map.Value.OffsetX, map.Value.SizeFactor);
        double standardY = MapCoordinateCalculator.ConvertWorldToMapCoordinate(posY, map.Value.OffsetY, map.Value.SizeFactor);

        return $"{source}=row:{map.Value.RowId} place:{placeName ?? "-"} size:{map.Value.SizeFactor} offsetX:{map.Value.OffsetX} offsetY:{map.Value.OffsetY} standard:X:{standardX:0.000}/Y:{standardY:0.000}";
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
        float rawY,
        string? resolutionSource,
        bool territoryTypeFound,
        bool territoryMapFound)
    {
        double mapX = MapCoordinateCalculator.ConvertWorldToMapCoordinate(rawX, map.OffsetX, map.SizeFactor);
        double mapY = MapCoordinateCalculator.ConvertWorldToMapCoordinate(rawY, map.OffsetY, map.SizeFactor);

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

    private sealed class MapResolutionCandidate
    {
        public string Source { get; init; } = string.Empty;

        public Map? Map { get; init; }

        public uint? TerritoryTypeId { get; init; }

        public string? MapName { get; init; }
    }

    private sealed class MapResolutionResult
    {
        public Map? Map { get; init; }

        public string? Source { get; init; }

        public uint? TerritoryTypeId { get; init; }

        public string? MapName { get; init; }

        public bool TerritoryTypeFound { get; init; }

        public bool TerritoryMapFound { get; init; }

        public string? Issue { get; init; }

        public static MapResolutionResult Success(
            Map map,
            string? source,
            uint? territoryTypeId,
            string? mapName,
            bool territoryTypeFound,
            bool territoryMapFound)
        {
            return new MapResolutionResult
            {
                Map = map,
                Source = source,
                TerritoryTypeId = territoryTypeId,
                MapName = mapName,
                TerritoryTypeFound = territoryTypeFound,
                TerritoryMapFound = territoryMapFound,
            };
        }

        public static MapResolutionResult Failed(
            MapResolutionCandidate? territoryCandidate,
            MapResolutionCandidate? mapIdCandidate,
            MapResolutionCandidate? mapNameCandidate,
            bool territoryTypeFound,
            bool territoryMapFound)
        {
            string issue = $"Map resolution failed. territoryType.Map={DescribeCandidate(territoryCandidate)}; mapId={DescribeCandidate(mapIdCandidate)}; mapName={DescribeCandidate(mapNameCandidate)}";

            return new MapResolutionResult
            {
                Map = null,
                Source = null,
                TerritoryTypeId = territoryCandidate?.TerritoryTypeId ?? mapNameCandidate?.TerritoryTypeId,
                MapName = territoryCandidate?.MapName ?? mapIdCandidate?.MapName ?? mapNameCandidate?.MapName,
                TerritoryTypeFound = territoryTypeFound,
                TerritoryMapFound = territoryMapFound,
                Issue = issue,
            };
        }

        private static string DescribeCandidate(MapResolutionCandidate? candidate)
        {
            if (candidate is null)
            {
                return "null";
            }

            if (candidate.Map is null)
            {
                return $"{candidate.Source}:map-null";
            }

            Map map = candidate.Map.Value;
            return $"{candidate.Source}:row={map.RowId},size={map.SizeFactor},offsetX={map.OffsetX},offsetY={map.OffsetY}";
        }
    }
}
