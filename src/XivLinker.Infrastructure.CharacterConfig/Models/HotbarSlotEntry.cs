namespace XivLinker.Infrastructure.CharacterConfig.Models;

public sealed record HotbarSlotEntry(
    uint CommandId,
    byte GroupId,
    byte HotbarId,
    byte SlotId,
    byte SlotTypeId);
