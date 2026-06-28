using Lumina;
using Lumina.Data;
using Lumina.Excel;
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
    private static readonly Language[] MapResolutionLanguages =
    [
        Language.Japanese,
        Language.English,
        Language.German,
        Language.French,
    ];
    private static readonly string[] KnownSqPackPaths =
    [
        @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
        @"C:\Program Files\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
    ];
    private const string MapResolveFailedText = "Lumina の Map を解決できませんでした。";

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
                DescribeMapCoordinateResolutionAcrossLanguages(data, mapId, territoryTypeId, mapName, rawX, rawY, rawZ));

            ResolvedMapLocation? location = await Task.Run(
                () => ResolveMapLocationCore(data, territoryTypeId, mapId, mapName, rawX, rawY, rawZ),
                cancellationToken);

            if (location is null)
            {
                logger.LogWarning(
                    "Map resolve returned null. TerritoryTypeId: {TerritoryTypeId}, CurrentMapID: {MapId}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}",
                    territoryTypeId,
                    mapId,
                    rawX,
                    rawY,
                    rawZ);
                return null;
            }

            logger.LogInformation(
                "Map coordinate conversion result. Source: {Source}, MapRowId: {MapRowId}, MapName: {MapName}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}, OffsetX: {OffsetX}, OffsetY: {OffsetY}, SizeFactor: {SizeFactor}, MapX: {MapX}, MapY: {MapY}, CoordinatesText: {CoordinatesText}",
                location.ResolutionSource,
                location.MapId,
                location.MapName,
                location.RawX,
                location.RawY,
                location.RawZ,
                location.OffsetX,
                location.OffsetY,
                location.SizeFactor,
                location.MapX,
                location.MapY,
                location.CoordinatesText);

            if (!string.IsNullOrWhiteSpace(location.IssueMessage))
            {
                logger.LogWarning(
                    "Map resolve issue. InputTerritoryTypeId: {TerritoryTypeId}, InputMapId: {MapId}, InputMapName: {MapName}, RawX: {RawX}, RawY: {RawY}, RawZ: {RawZ}, Issue: {IssueMessage}",
                    territoryTypeId,
                    mapId,
                    mapName,
                    rawX,
                    rawY,
                    rawZ,
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
                CoordinatesText = MapCoordinateCalculator.CoordinateConversionFailedText,
                IssueMessage = "マップ情報の解決に失敗しました。",
                RawX = rawX,
                RawY = rawY,
                RawZ = rawZ,
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
        float rawY,
        float rawZ)
    {
        MapResolutionResult? failedResult = null;

        foreach (Language language in MapResolutionLanguages)
        {
            MapResolutionResult resolution = ResolveMapRow(
                data,
                mapId,
                territoryTypeId,
                requestedMapName,
                language);

            if (resolution.Map is not null)
            {
                string? resolvedMapName = resolution.MapName ?? requestedMapName;
                return CreateResolvedMapLocation(
                    resolution.Map.Value,
                    territoryTypeId,
                    resolvedMapName,
                    rawX,
                    rawY,
                    rawZ,
                    $"{resolution.Source} ({language})",
                    resolution.TerritoryTypeFound,
                    resolution.TerritoryMapFound);
            }

            failedResult ??= resolution;
        }

        string? fallbackMapName = failedResult?.MapName ?? requestedMapName;
        return new ResolvedMapLocation
        {
            MapId = mapId ?? 0,
            MapName = string.IsNullOrWhiteSpace(fallbackMapName)
                ? $"Territory ID: {territoryTypeId ?? 0}"
                : fallbackMapName,
            ResolutionSource = failedResult?.Source,
            TerritoryTypeFound = failedResult?.TerritoryTypeFound ?? false,
            TerritoryMapFound = failedResult?.TerritoryMapFound ?? false,
            CoordinatesText = MapCoordinateCalculator.CoordinateConversionFailedText,
            IssueMessage = failedResult?.Issue
                ?? $"{MapResolveFailedText} TerritoryTypeId={territoryTypeId}, CurrentMapID={mapId}",
            RawX = rawX,
            RawY = rawY,
            RawZ = rawZ,
        };
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

        if (territoryCandidate?.Map is Map resolvedTerritoryMap
            && IsUsableMapForCoordinates(resolvedTerritoryMap))
        {
            return MapResolutionResult.Success(
                resolvedTerritoryMap,
                territoryCandidate.Source,
                territoryCandidate.TerritoryTypeId,
                territoryCandidate.MapName,
                territoryTypeFound,
                territoryMapFound);
        }

        MapResolutionCandidate? mapIdCandidate = ResolveMapRowFromMapId(data, mapId, language);
        if (mapIdCandidate?.Map is Map resolvedMapIdMap
            && IsUsableMapForCoordinates(resolvedMapIdMap))
        {
            return MapResolutionResult.Success(
                resolvedMapIdMap,
                mapIdCandidate.Source,
                mapIdCandidate.TerritoryTypeId,
                mapIdCandidate.MapName,
                territoryTypeFound,
                territoryMapFound);
        }

        MapResolutionCandidate? mapNameCandidate = ResolveMapRowByMapName(data, mapName, language);
        if (mapNameCandidate?.Map is Map resolvedMapNameMap
            && IsUsableMapForCoordinates(resolvedMapNameMap))
        {
            return MapResolutionResult.Success(
                resolvedMapNameMap,
                mapNameCandidate.Source,
                mapNameCandidate.TerritoryTypeId ?? territoryCandidate?.TerritoryTypeId,
                mapNameCandidate.MapName,
                territoryTypeFound,
                territoryMapFound);
        }

        MapResolutionCandidate? mapPlaceNameCandidate = ResolveMapRowByMapPlaceName(data, mapName, language);
        if (mapPlaceNameCandidate?.Map is Map resolvedMapPlaceNameMap
            && IsUsableMapForCoordinates(resolvedMapPlaceNameMap))
        {
            return MapResolutionResult.Success(
                resolvedMapPlaceNameMap,
                mapPlaceNameCandidate.Source,
                mapPlaceNameCandidate.TerritoryTypeId ?? territoryCandidate?.TerritoryTypeId,
                mapPlaceNameCandidate.MapName,
                territoryTypeFound,
                territoryMapFound);
        }

        return MapResolutionResult.Failed(
            territoryTypeId,
            mapId,
            mapName,
            territoryCandidate,
            mapIdCandidate,
            mapNameCandidate,
            mapPlaceNameCandidate,
            territoryTypeFound,
            territoryMapFound);
    }

    private static MapResolutionCandidate? ResolveMapRowFromTerritoryType(GameData data, uint? territoryTypeId, Language language)
    {
        if (territoryTypeId is null or 0)
        {
            return null;
        }

        ExcelSheet<TerritoryType>? territorySheet = data.GetExcelSheet<TerritoryType>(language, null);
        if (territorySheet is null)
        {
            return null;
        }

        TerritoryType? territory = territorySheet.GetRowOrDefault(territoryTypeId.Value);

        if (territory is null || territory.Value.RowId == 0)
        {
            return null;
        }

        string? placeName = ExtractPlaceName(territory.Value.PlaceNameZone.ValueNullable)
            ?? ExtractPlaceName(territory.Value.PlaceName.ValueNullable);
        Map? resolvedMap = ResolveMapFromReference(data, territory.Value.Map, language);

        return new MapResolutionCandidate
        {
            Source = "territoryType.Map",
            Map = resolvedMap,
            TerritoryTypeId = territory.Value.RowId,
            MapName = placeName,
            TerritoryRowFound = true,
            MapReferenceType = territory.Value.Map.GetType().FullName,
            MapReferenceRowId = ReadReferenceRowId(territory.Value.Map),
            MapReferenceValueFound = resolvedMap is not null,
        };
    }

    private static MapResolutionCandidate? ResolveMapRowFromMapId(GameData data, uint? mapId, Language language)
    {
        if (mapId is null or 0)
        {
            return null;
        }

        ExcelSheet<Map>? mapSheet = data.GetExcelSheet<Map>(language, null);
        if (mapSheet is null)
        {
            return null;
        }

        Map? map = mapSheet.GetRowOrDefault(mapId.Value);

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
        ExcelSheet<TerritoryType>? territorySheet = data.GetExcelSheet<TerritoryType>(language, null);
        if (territorySheet is null)
        {
            return null;
        }

        foreach (TerritoryType territoryType in territorySheet)
        {
            string? placeName = ExtractPlaceName(territoryType.PlaceName.ValueNullable);
            string? placeNameZone = ExtractPlaceName(territoryType.PlaceNameZone.ValueNullable);

            bool matches =
                string.Equals(
                    NormalizeComparisonText(placeName),
                    normalizedTargetName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    NormalizeComparisonText(placeNameZone),
                    normalizedTargetName,
                    StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                continue;
            }

            Map? map = ResolveMapFromReference(data, territoryType.Map, language);
            if (map is not null && map.Value.RowId != 0)
            {
                string? resolvedName = !string.IsNullOrWhiteSpace(placeNameZone)
                    ? placeNameZone
                    : placeName;

                return new MapResolutionCandidate
                {
                    Source = "mapName",
                    Map = map.Value,
                    TerritoryTypeId = territoryType.RowId,
                    MapName = resolvedName,
                    TerritoryRowFound = true,
                    MapReferenceType = territoryType.Map.GetType().FullName,
                    MapReferenceRowId = ReadReferenceRowId(territoryType.Map),
                    MapReferenceValueFound = map is not null,
                };
            }
        }

        return null;
    }

    private static MapResolutionCandidate? ResolveMapRowByMapPlaceName(GameData data, string? mapName, Language language)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return null;
        }

        string normalizedTargetName = NormalizeComparisonText(mapName);
        ExcelSheet<Map>? mapSheet = data.GetExcelSheet<Map>(language, null);
        if (mapSheet is null)
        {
            return null;
        }

        foreach (Map map in mapSheet)
        {
            string? placeName = ExtractPlaceName(map.PlaceName.ValueNullable);
            if (!string.Equals(
                NormalizeComparisonText(placeName),
                normalizedTargetName,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (map.RowId == 0)
            {
                continue;
            }

            return new MapResolutionCandidate
            {
                Source = "map.PlaceName",
                Map = map,
                MapName = placeName,
            };
        }

        return null;
    }

    private static bool IsUsableMapForCoordinates(Map map)
    {
        return map.RowId != 0 && map.SizeFactor > 0;
    }

    private static string NormalizeComparisonText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim();
    }

    private static string DescribeMapCoordinateResolutionAcrossLanguages(
        GameData data,
        uint? mapId,
        uint? territoryTypeId,
        string? mapName,
        double rawX,
        double rawY,
        double rawZ)
    {
        return string.Join(
            " || ",
            MapResolutionLanguages.Select(language =>
                $"{language}: {DescribeMapCoordinateResolution(data, mapId, territoryTypeId, mapName, rawX, rawY, rawZ, language)}"));
    }

    private static string DescribeMapCoordinateResolution(
        GameData data,
        uint? mapId,
        uint? territoryTypeId,
        string? mapName,
        double rawX,
        double rawY,
        double rawZ,
        Language language)
    {
        string territoryMapDescription = DescribeResolvedMapRow(
            "territoryType.Map",
            ResolveMapRowFromTerritoryType(data, territoryTypeId, language)?.Map,
            rawX,
            rawY,
            rawZ);
        string directMapDescription = DescribeResolvedMapRow(
            "mapId",
            ResolveMapRowFromMapId(data, mapId, language)?.Map,
            rawX,
            rawY,
            rawZ);
        string nameMapDescription = DescribeResolvedMapRow(
            "mapName",
            ResolveMapRowByMapName(data, mapName, language)?.Map,
            rawX,
            rawY,
            rawZ);
        string mapPlaceNameDescription = DescribeResolvedMapRow(
            "map.PlaceName",
            ResolveMapRowByMapPlaceName(data, mapName, language)?.Map,
            rawX,
            rawY,
            rawZ);

        MapResolutionResult final = ResolveMapRow(data, mapId, territoryTypeId, mapName, language);
        string finalDescription = DescribeResolvedMapRow("selected", final.Map, rawX, rawY, rawZ);

        return $"input(mapId={mapId?.ToString() ?? "-"}, territoryTypeId={territoryTypeId?.ToString() ?? "-"}, mapName={mapName ?? "-"}) | {territoryMapDescription} | {directMapDescription} | {nameMapDescription} | {mapPlaceNameDescription} | {finalDescription}";
    }

    private static string DescribeResolvedMapRow(
        string source,
        Map? map,
        double rawX,
        double rawY,
        double rawZ)
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

        double standardX = MapCoordinateCalculator.ConvertWorldToMapCoordinate(
            rawX,
            map.Value.OffsetX,
            map.Value.SizeFactor);
        double standardY = MapCoordinateCalculator.ConvertWorldToMapCoordinate(
            rawY,
            map.Value.OffsetY,
            map.Value.SizeFactor);
        double zBasedY = MapCoordinateCalculator.ConvertWorldToMapCoordinate(
            rawZ,
            map.Value.OffsetY,
            map.Value.SizeFactor);

        return $"{source}=row:{map.Value.RowId} place:{placeName ?? "-"} size:{map.Value.SizeFactor} offsetX:{map.Value.OffsetX} offsetY:{map.Value.OffsetY} rawX:{rawX:0.###} rawY:{rawY:0.###} rawZ:{rawZ:0.###} standard:X:{standardX:0.000}/Y:{standardY:0.000} zBased:X:{standardX:0.000}/Y:{zBasedY:0.000}";
    }

    private static ResolvedClassJobInfo? ResolveClassJobCore(GameData data, uint classJobId, int? level)
    {
        IEnumerable<ClassJob>? classJobSheet = data.GetExcelSheet<ClassJob>(Language.Japanese);
        if (classJobSheet is null)
        {
            return null;
        }

        ClassJob? classJob = null;
        foreach (ClassJob row in classJobSheet)
        {
            if (row.RowId == classJobId)
            {
                classJob = row;
                break;
            }
        }

        if (classJob is null || classJob.Value.RowId == 0)
        {
            return null;
        }

        string jobName = ExtractTextSafely(classJob.Value.Name) ?? string.Empty;
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
        float rawZ,
        string? resolutionSource,
        bool territoryTypeFound,
        bool territoryMapFound)
    {
        double mapX = MapCoordinateCalculator.ConvertWorldToMapCoordinate(
            rawX,
            map.OffsetX,
            map.SizeFactor);
        double mapY = MapCoordinateCalculator.ConvertWorldToMapCoordinate(
            rawY,
            map.OffsetY,
            map.SizeFactor);

        return new ResolvedMapLocation
        {
            MapId = map.RowId,
            MapName = string.IsNullOrWhiteSpace(mapName)
                ? $"Territory ID: {territoryTypeId ?? 0}"
                : mapName,
            ResolutionSource = resolutionSource,
            TerritoryTypeFound = territoryTypeFound,
            TerritoryMapFound = territoryMapFound,
            OffsetX = map.OffsetX,
            OffsetY = map.OffsetY,
            SizeFactor = map.SizeFactor,
            RawX = rawX,
            RawY = rawY,
            RawZ = rawZ,
            MapX = mapX,
            MapY = mapY,
            CoordinatesText = MapCoordinateCalculator.FormatCoordinates(mapX, mapY),
        };
    }

    private static string? ExtractPlaceName(PlaceName? placeName)
    {
        return placeName is null ? null : ExtractTextSafely(placeName.Value.Name);
    }

    private static Map? ResolveMapFromReference(GameData data, object? mapReference, Language language)
    {
        if (mapReference is null)
        {
            return null;
        }

        if (mapReference is Map directMap && directMap.RowId != 0)
        {
            return directMap;
        }

        object? value = mapReference.GetType().GetProperty("Value")?.GetValue(mapReference);
        if (value is Map valueMap && valueMap.RowId != 0)
        {
            return valueMap;
        }

        object? valueNullable = mapReference.GetType().GetProperty("ValueNullable")?.GetValue(mapReference);
        if (valueNullable is Map nullableMap && nullableMap.RowId != 0)
        {
            return nullableMap;
        }

        uint? rowId = ReadReferenceRowId(mapReference);
        if (rowId is null or 0)
        {
            return null;
        }

        ExcelSheet<Map>? mapSheet = data.GetExcelSheet<Map>(language, null);
        return mapSheet?.GetRowOrDefault(rowId.Value);
    }

    private static uint? ReadReferenceRowId(object? mapReference)
    {
        if (mapReference is null)
        {
            return null;
        }

        object? rowId = mapReference.GetType().GetProperty("RowId")?.GetValue(mapReference);
        return TryConvertToUInt(rowId, out uint result) ? result : null;
    }

    private static bool TryConvertToUInt(object? value, out uint result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case int intValue when intValue >= 0:
                result = (uint)intValue;
                return true;
            case long longValue when longValue >= 0 && longValue <= uint.MaxValue:
                result = (uint)longValue;
                return true;
            default:
                result = 0;
                return false;
        }
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

        public bool TerritoryRowFound { get; init; }

        public string? MapReferenceType { get; init; }

        public uint? MapReferenceRowId { get; init; }

        public bool MapReferenceValueFound { get; init; }
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
            uint? inputTerritoryTypeId,
            uint? inputMapId,
            string? inputMapName,
            MapResolutionCandidate? territoryCandidate,
            MapResolutionCandidate? mapIdCandidate,
            MapResolutionCandidate? mapNameCandidate,
            MapResolutionCandidate? mapPlaceNameCandidate,
            bool territoryTypeFound,
            bool territoryMapFound)
        {
            string issue =
                $"Map resolution failed. inputTerritoryTypeId={inputTerritoryTypeId?.ToString() ?? "null"}; " +
                $"inputMapId={inputMapId?.ToString() ?? "null"}; " +
                $"inputMapName={inputMapName ?? "null"}; " +
                $"territoryRowFound={territoryCandidate?.TerritoryRowFound.ToString() ?? "false"}; " +
                $"territoryMapReferenceType={territoryCandidate?.MapReferenceType ?? "null"}; " +
                $"territoryMapValueFound={territoryCandidate?.MapReferenceValueFound.ToString() ?? "false"}; " +
                $"territoryMapRowId={territoryCandidate?.MapReferenceRowId?.ToString() ?? "null"}; " +
                $"territoryMapRowResolved={(territoryCandidate?.Map is not null).ToString()}; " +
                $"mapNameCandidateFound={(mapNameCandidate is not null).ToString()}; " +
                $"mapPlaceNameCandidateFound={(mapPlaceNameCandidate is not null).ToString()}; " +
                $"selectedSource=null; " +
                $"selectedMapRowId=null; " +
                $"territoryType.Map={DescribeCandidate(territoryCandidate)}; " +
                $"mapId={DescribeCandidate(mapIdCandidate)}; " +
                $"mapName={DescribeCandidate(mapNameCandidate)}; " +
                $"map.PlaceName={DescribeCandidate(mapPlaceNameCandidate)}";

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
