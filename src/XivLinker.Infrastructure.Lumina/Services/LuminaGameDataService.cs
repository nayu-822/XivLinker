using Lumina;
using Microsoft.Extensions.Options;

namespace XivLinker.Infrastructure.Lumina.Services;

public sealed class LuminaGameDataService : IGameDataService, IDisposable
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
    private GameData? gameData;
    private string? resolvedSqPackPath;

    public LuminaGameDataService(IOptions<LuminaOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SqPackPath);

    public bool IsAvailable => gameData is not null;

    public string? SqPackPath => resolvedSqPackPath ??= ResolveSqPackPath();

    public string? ErrorMessage { get; private set; }

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
}
