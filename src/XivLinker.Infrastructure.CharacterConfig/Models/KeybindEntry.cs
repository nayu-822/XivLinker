namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record KeybindEntry(
    string Command,
    KeybindGesture? Primary,
    KeybindGesture? Secondary);

public sealed record KeybindGesture(
    string Key,
    string Modifier,
    string DisplayText,
    IReadOnlyList<string> Keys);
