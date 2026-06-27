namespace XivLinker.App.ViewModels;

public sealed class CharacterProfileItemViewModel
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string CharacterSettingsDirectory { get; init; }

    public required string HotbarDatPath { get; init; }

    public required string KeybindDatPath { get; init; }

    public required bool IsSelected { get; init; }

    public required string LastLoadedAtText { get; init; }
}
