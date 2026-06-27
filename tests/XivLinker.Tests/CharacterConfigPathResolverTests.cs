using XivLinker.Infrastructure.CharacterConfig.Models;
using XivLinker.Infrastructure.CharacterConfig.Services;

namespace XivLinker.Tests;

public sealed class CharacterConfigPathResolverTests
{
    [Fact]
    public void ResolveHotbarDatPath_UsesCharacterDirectory_WhenExplicitPathIsNotSet()
    {
        var profile = new CharacterProfile
        {
            CharacterSettingsDirectory = @"C:\ff14\CHR0000000000000000",
        };

        string path = CharacterConfigPathResolver.ResolveHotbarDatPath(profile);

        Assert.Equal(
            Path.Combine(profile.CharacterSettingsDirectory, "HOTBAR.DAT"),
            path);
    }

    [Fact]
    public void ResolveKeybindDatPath_UsesExplicitFilePath_WhenProvided()
    {
        var profile = new CharacterProfile
        {
            CharacterSettingsDirectory = @"C:\ff14\CHR0000000000000000",
            KeybindDatPath = @"D:\backup\KEYBIND.DAT",
        };

        string path = CharacterConfigPathResolver.ResolveKeybindDatPath(profile);

        Assert.Equal(profile.KeybindDatPath, path);
    }
}
