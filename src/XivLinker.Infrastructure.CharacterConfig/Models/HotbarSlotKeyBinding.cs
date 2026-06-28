namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record HotbarSlotKeyBinding(
    int HotbarNumber,
    int SlotNumber,
    string Command,
    string KeyGestureText,
    IReadOnlyList<string> Keys);
