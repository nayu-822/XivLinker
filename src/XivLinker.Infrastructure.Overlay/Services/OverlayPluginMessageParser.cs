using System.Globalization;
using System.Text.Json;
using XivLinker.Infrastructure.Overlay.Models;

namespace XivLinker.Infrastructure.Overlay.Services;

public static class OverlayPluginMessageParser
{
    public static bool TryParseEventMessage(string rawJson, out OverlayPluginEventMessage? message)
    {
        message = null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);
            JsonElement root = document.RootElement;

            string? rootType = ReadString(root, "type");
            if (HasSequenceNumber(root))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(rootType)
                && !string.Equals(rootType, "broadcast", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rootType, "event", StringComparison.OrdinalIgnoreCase)
                && LooksLikeOverlayEventType(rootType))
            {
                message = new OverlayPluginEventMessage
                {
                    MessageType = rootType,
                    Payload = root.Clone(),
                    RawJson = rawJson,
                };

                return true;
            }

            JsonElement payload = GetPayload(root);
            string? messageType = ReadEventName(root, allowType: false)
                ?? ReadEventName(payload, allowType: true);

            if (string.IsNullOrWhiteSpace(messageType)
                && !string.IsNullOrWhiteSpace(rootType)
                && !string.Equals(rootType, "broadcast", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rootType, "event", StringComparison.OrdinalIgnoreCase)
                && !HasSequenceNumber(root))
            {
                messageType = rootType;
            }

            if (string.IsNullOrWhiteSpace(messageType))
            {
                return false;
            }

            message = new OverlayPluginEventMessage
            {
                MessageType = messageType,
                Payload = payload,
                RawJson = rawJson,
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryParseChangeZone(OverlayPluginEventMessage message, out uint territoryTypeId, out string zoneName)
    {
        territoryTypeId = 0;
        zoneName = string.Empty;

        if (!string.Equals(message.MessageType, "ChangeZone", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryReadChangeZonePayload(message.Payload, out territoryTypeId, out zoneName))
        {
            return true;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(message.RawJson);
            JsonElement root = document.RootElement;

            if (TryReadChangeZonePayload(root, out territoryTypeId, out zoneName))
            {
                return true;
            }

            if (TryGetPropertyIgnoreCase(root, "msg", out JsonElement msg)
                && msg.ValueKind == JsonValueKind.Object
                && TryReadChangeZonePayload(msg, out territoryTypeId, out zoneName))
            {
                return true;
            }

            if (TryGetPropertyIgnoreCase(root, "payload", out JsonElement payload)
                && payload.ValueKind == JsonValueKind.Object
                && TryReadChangeZonePayload(payload, out territoryTypeId, out zoneName))
            {
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryReadChangeZonePayload(JsonElement payload, out uint territoryTypeId, out string zoneName)
    {
        territoryTypeId = ReadUInt32(payload, "zoneID")
            ?? ReadUInt32(payload, "zoneId")
            ?? ReadUInt32(payload, "ZoneID")
            ?? ReadUInt32(payload, "territoryType")
            ?? ReadUInt32(payload, "territoryTypeId")
            ?? ReadUInt32(payload, "territoryTypeID")
            ?? ReadUInt32(payload, "territoryId")
            ?? ReadUInt32(payload, "territoryID")
            ?? ReadUInt32(payload, "TerritoryType")
            ?? ReadUInt32(payload, "TerritoryTypeId")
            ?? ReadUInt32(payload, "TerritoryTypeID")
            ?? ReadUInt32(payload, "Territory")
            ?? 0;
        zoneName = ReadString(payload, "zoneName")
            ?? ReadString(payload, "ZoneName")
            ?? ReadString(payload, "placeName")
            ?? ReadString(payload, "PlaceName")
            ?? ReadString(payload, "name")
            ?? ReadString(payload, "Name")
            ?? string.Empty;

        return territoryTypeId > 0 || !string.IsNullOrWhiteSpace(zoneName);
    }

    public static bool TryParsePrimaryPlayer(OverlayPluginEventMessage message, out string playerName)
    {
        playerName = string.Empty;

        if (!string.Equals(message.MessageType, "ChangePrimaryPlayer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        playerName = ReadString(message.Payload, "charName")
            ?? ReadString(message.Payload, "charname")
            ?? ReadString(message.Payload, "name")
            ?? ReadString(message.Payload, "Name")
            ?? ReadString(message.Payload, "playerName")
            ?? ReadString(message.Payload, "PlayerName")
            ?? string.Empty;

        return !string.IsNullOrWhiteSpace(playerName);
    }

    public static OverlayCurrentPlayerSnapshot? TryParseCurrentPlayerSnapshot(string rawJson, string? playerName = null)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("combatants", out JsonElement combatantsElement)
                || combatantsElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            JsonElement? selectedCombatant = null;

            foreach (JsonElement combatant in combatantsElement.EnumerateArray())
            {
                string? candidateName = ReadString(combatant, "Name");
                if (!string.IsNullOrWhiteSpace(playerName)
                    && string.Equals(candidateName, playerName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedCombatant = combatant;
                    break;
                }

                if (selectedCombatant is null && LooksLikeCurrentPlayer(combatant))
                {
                    selectedCombatant = combatant;
                }
            }

            if (selectedCombatant is null)
            {
                return null;
            }

            JsonElement current = selectedCombatant.Value;
            string resolvedName = ReadString(current, "Name") ?? playerName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                return null;
            }

            return new OverlayCurrentPlayerSnapshot
            {
                PlayerName = resolvedName,
                RawCombatantJson = current.GetRawText(),
                CombatantTerritoryTypeId = ReadUInt32(current, "CurrentZoneID")
                    ?? ReadUInt32(current, "CurrentZoneId")
                    ?? ReadUInt32(current, "TerritoryType")
                    ?? ReadUInt32(current, "territoryType")
                    ?? ReadUInt32(current, "territoryTypeId"),
                CombatantMapId = ReadUInt32(current, "CurrentMapID")
                    ?? ReadUInt32(current, "CurrentMapId")
                    ?? ReadUInt32(current, "MapID")
                    ?? ReadUInt32(current, "MapId")
                    ?? ReadUInt32(current, "mapId"),
                RawX = ReadSingle(current, "PosX")
                    ?? ReadSingle(current, "posX")
                    ?? ReadSingle(current, "X")
                    ?? ReadSingle(current, "x")
                    ?? ReadSingle(current, "WorldX")
                    ?? ReadSingle(current, "worldX")
                    ?? 0,
                RawY = ReadSingle(current, "PosY")
                    ?? ReadSingle(current, "posY")
                    ?? ReadSingle(current, "Y")
                    ?? ReadSingle(current, "y")
                    ?? ReadSingle(current, "WorldY")
                    ?? ReadSingle(current, "worldY")
                    ?? 0,
                RawZ = ReadSingle(current, "PosZ")
                    ?? ReadSingle(current, "posZ")
                    ?? ReadSingle(current, "Z")
                    ?? ReadSingle(current, "z")
                    ?? ReadSingle(current, "WorldZ")
                    ?? ReadSingle(current, "worldZ")
                    ?? 0,
                ClassJobId = ReadUInt32(current, "Job") ?? ReadUInt32(current, "ClassJob"),
                Level = ReadInt32(current, "Level"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool LooksLikeCurrentPlayer(JsonElement combatant)
    {
        string? name = ReadString(combatant, "Name");
        uint? bNpcId = ReadUInt32(combatant, "BNpcID");
        uint? ownerId = ReadUInt32(combatant, "OwnerID");
        uint? job = ReadUInt32(combatant, "Job") ?? ReadUInt32(combatant, "ClassJob");

        return !string.IsNullOrWhiteSpace(name)
            && (bNpcId is null or 0)
            && (ownerId is null or 0)
            && (job is null || job > 0);
    }

    private static uint? ReadUInt32(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetUInt32(out uint value) => value,
            JsonValueKind.String when uint.TryParse(property.GetString(), out uint value) => value,
            _ => null,
        };
    }

    private static int? ReadInt32(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out int value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out int value) => value,
            _ => null,
        };
    }

    private static float? ReadSingle(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetSingle(out float value) => value,
            JsonValueKind.String when float.TryParse(
                property.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value) => value,
            JsonValueKind.String when float.TryParse(property.GetString(), out float fallbackValue) => fallbackValue,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static JsonElement GetPayload(JsonElement root)
    {
        if (TryGetPropertyIgnoreCase(root, "msg", out JsonElement msgElement))
        {
            return msgElement.Clone();
        }

        if (TryGetPropertyIgnoreCase(root, "payload", out JsonElement payloadElement))
        {
            return payloadElement.Clone();
        }

        return root.Clone();
    }

    private static string? ReadEventName(JsonElement element, bool allowType)
    {
        string? value = ReadString(element, "msgtype")
            ?? ReadString(element, "event")
            ?? ReadString(element, "name");

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!allowType)
        {
            return null;
        }

        string? type = ReadString(element, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        return string.Equals(type, "broadcast", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "event", StringComparison.OrdinalIgnoreCase)
            ? null
            : type;
    }

    private static bool HasSequenceNumber(JsonElement element)
    {
        return TryGetPropertyIgnoreCase(element, "rseq", out _);
    }

    private static bool LooksLikeOverlayEventType(string type)
    {
        return string.Equals(type, "ChangeZone", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "ChangePrimaryPlayer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "LogLine", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "ChangeMap", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        foreach (JsonProperty candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
