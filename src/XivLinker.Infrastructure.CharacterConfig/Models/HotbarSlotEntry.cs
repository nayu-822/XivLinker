namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record HotbarSlotEntry(
    int HotbarNumber,
    int SlotNumber,
    HotbarSlotKind Kind,
    uint ActionOrCommandId,
    uint? ClassJobId,
    bool IsShared);
