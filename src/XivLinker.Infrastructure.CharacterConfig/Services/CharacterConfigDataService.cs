using Microsoft.Extensions.Logging;
using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CharacterConfigDataService : ICharacterConfigDataService
{
    private readonly ILogger<CharacterConfigDataService> logger;

    public CharacterConfigDataService(ILogger<CharacterConfigDataService> logger)
    {
        this.logger = logger;
    }

    public async Task<CharacterData> LoadAsync(CharacterProfile profile, CancellationToken cancellationToken = default)
    {
        string hotbarPath = CharacterConfigPathResolver.ResolveHotbarDatPath(profile);
        string keybindPath = CharacterConfigPathResolver.ResolveKeybindDatPath(profile);

        var errors = new List<string>();
        HotbarAnalysisResult hotbarResult = await LoadHotbarAsync(hotbarPath, errors, cancellationToken);
        KeybindAnalysisResult keybindResult = await LoadKeybindAsync(keybindPath, errors, cancellationToken);

        return new CharacterData
        {
            Profile = profile,
            CharacterDirectoryPath = profile.CharacterSettingsDirectory,
            HotbarAnalysisResult = hotbarResult,
            KeybindAnalysisResult = keybindResult,
            LoadedAt = DateTimeOffset.Now,
            Errors = errors,
        };
    }

    private async Task<HotbarAnalysisResult> LoadHotbarAsync(
        string path,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        return new HotbarAnalysisResult
        {
            FilePath = path,
            Exists = File.Exists(path),
            ByteLength = await TryReadLengthAsync(path, "HOTBAR.DAT", errors, cancellationToken),
            LastWriteTime = TryGetLastWriteTime(path),
            RawBytes = await TryReadBytesAsync(path, "HOTBAR.DAT", errors, cancellationToken),
        };
    }

    private async Task<KeybindAnalysisResult> LoadKeybindAsync(
        string path,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        return new KeybindAnalysisResult
        {
            FilePath = path,
            Exists = File.Exists(path),
            ByteLength = await TryReadLengthAsync(path, "KEYBIND.DAT", errors, cancellationToken),
            LastWriteTime = TryGetLastWriteTime(path),
            RawBytes = await TryReadBytesAsync(path, "KEYBIND.DAT", errors, cancellationToken),
        };
    }

    private async Task<long> TryReadLengthAsync(
        string path,
        string logicalName,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        byte[]? bytes = await TryReadBytesAsync(path, logicalName, errors, cancellationToken);
        return bytes?.LongLength ?? 0;
    }

    private async Task<byte[]?> TryReadBytesAsync(
        string path,
        string logicalName,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            errors.Add($"{logicalName} が見つかりません: {path}");
            return null;
        }

        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Failed to read character config file {LogicalName}: {Path}", logicalName, path);
            errors.Add($"{logicalName} の読み込みに失敗しました: {path}");
            return null;
        }
    }

    private static DateTimeOffset? TryGetLastWriteTime(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return File.GetLastWriteTimeUtc(path);
    }
}
