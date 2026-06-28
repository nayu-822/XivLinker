namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record HotbarSlotKeyBinding(
    int HotbarNumber,
    int SlotNumber,
    string KeyGestureText,
    IReadOnlyList<string> Keys);
