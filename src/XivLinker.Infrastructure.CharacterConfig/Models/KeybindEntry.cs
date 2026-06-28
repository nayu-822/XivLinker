namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record KeybindEntry(
    string Command,
    KeybindGesture? Primary,
    KeybindGesture? Secondary)
{
    public char CommandSectionType { get; init; }

    public char BindingSectionType { get; init; }

    public string RawBindingText { get; init; } = string.Empty;

    public bool HasPrimaryOrSecondary =>
        Primary?.IsAssigned == true || Secondary?.IsAssigned == true;
}

public sealed record KeybindGesture(
    string KeyCode,
    string ModifierCode)
{
    public bool IsAssigned => !string.IsNullOrWhiteSpace(KeyCode);
}
