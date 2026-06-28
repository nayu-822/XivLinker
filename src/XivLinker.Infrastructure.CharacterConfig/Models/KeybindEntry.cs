namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record KeybindEntry(
    string Command,
    KeybindGesture? Primary,
    KeybindGesture? Secondary)
{
    public bool HasPrimaryOrSecondary =>
        (Primary?.Keys.Count ?? 0) > 0
        || (Secondary?.Keys.Count ?? 0) > 0;
}

public sealed record KeybindGesture(
    string Key,
    string Modifier,
    string DisplayText,
    IReadOnlyList<string> Keys);
