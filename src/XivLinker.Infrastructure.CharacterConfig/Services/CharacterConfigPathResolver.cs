using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public static class CharacterConfigPathResolver
{
    public static string ResolveHotbarDatPath(CharacterProfile profile)
    {
        return ResolveDatPath(profile.CharacterSettingsDirectory, profile.HotbarDatPath, "HOTBAR.DAT");
    }

    public static string ResolveKeybindDatPath(CharacterProfile profile)
    {
        return ResolveDatPath(profile.CharacterSettingsDirectory, profile.KeybindDatPath, "KEYBIND.DAT");
    }

    private static string ResolveDatPath(
        string characterSettingsDirectory,
        string? explicitPath,
        string fileName)
    {
        string? resolvedExplicit = ResolveCandidate(explicitPath, fileName);
        if (!string.IsNullOrWhiteSpace(resolvedExplicit))
        {
            return resolvedExplicit;
        }

        string? resolvedDirectory = ResolveCandidate(characterSettingsDirectory, fileName);
        return resolvedDirectory ?? Path.Combine(characterSettingsDirectory, fileName);
    }

    private static string? ResolveCandidate(string? path, string fileName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(path);

        if (Directory.Exists(fullPath))
        {
            return Path.Combine(fullPath, fileName);
        }

        string candidateFileName = Path.GetFileName(fullPath);
        if (candidateFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return null;
    }
}
