namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed class CharacterProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = string.Empty;

    public string CharacterSettingsDirectory { get; set; } = string.Empty;

    public string? HotbarDatPath { get; set; }

    public string? KeybindDatPath { get; set; }

    public bool IsSelected { get; set; }

    public DateTimeOffset? LastLoadedAt { get; set; }
}
