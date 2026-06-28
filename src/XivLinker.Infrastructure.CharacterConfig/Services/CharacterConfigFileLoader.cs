using XivLinker.Infrastructure.CharacterConfig.Models;

namespace XivLinker.Infrastructure.CharacterConfig.Services;

public sealed class CharacterConfigFileLoader
{
    private readonly ICharacterProfileStore characterProfileStore;

    public CharacterConfigFileLoader(ICharacterProfileStore characterProfileStore)
    {
        this.characterProfileStore = characterProfileStore;
    }

    public async Task<CharacterConfigFiles> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        CharacterProfile? profile = characterProfileStore.SelectedProfile;
        if (profile is null || string.IsNullOrWhiteSpace(profile.CharacterSettingsDirectory))
        {
            throw new InvalidOperationException("キャラクター設定ディレクトリが選択されていません。");
        }

        string hotbarPath = CharacterConfigPathResolver.ResolveHotbarDatPath(profile);
        string keybindPath = CharacterConfigPathResolver.ResolveKeybindDatPath(profile);

        byte[] hotbarBytes = await File.ReadAllBytesAsync(hotbarPath, cancellationToken);
        byte[] keybindBytes = await File.ReadAllBytesAsync(keybindPath, cancellationToken);

        return new CharacterConfigFiles
        {
            CharacterDirectoryPath = profile.CharacterSettingsDirectory,
            HotbarPath = hotbarPath,
            KeybindPath = keybindPath,
            HotbarBytes = hotbarBytes,
            KeybindBytes = keybindBytes,
        };
    }
}
