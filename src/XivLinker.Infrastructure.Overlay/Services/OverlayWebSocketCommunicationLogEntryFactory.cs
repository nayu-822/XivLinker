using System.Text.Json;
using XivLinker.Infrastructure.Overlay.Models;

namespace XivLinker.Infrastructure.Overlay.Services;

public static class OverlayWebSocketCommunicationLogEntryFactory
{
    public static OverlayWebSocketCommunicationLogEntry Create(
        string direction,
        string rawJson,
        string? fallbackName = null,
        DateTimeOffset? timestamp = null)
    {
        DateTimeOffset resolvedTimestamp = timestamp ?? DateTimeOffset.Now;

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawJson);
            JsonElement root = document.RootElement;
            long? rseq = TryReadInt64(root, "rseq");
            string kind = ResolveKind(root, rseq);
            string name = ResolveName(root, kind, fallbackName);

            return new OverlayWebSocketCommunicationLogEntry
            {
                Timestamp = resolvedTimestamp,
                Direction = direction,
                Kind = kind,
                Name = name,
                SequenceNumber = rseq,
                RawJson = rawJson,
            };
        }
        catch (JsonException)
        {
            return new OverlayWebSocketCommunicationLogEntry
            {
                Timestamp = resolvedTimestamp,
                Direction = direction,
                Kind = "invalid json",
                Name = fallbackName ?? string.Empty,
                SequenceNumber = null,
                RawJson = rawJson,
            };
        }
    }

    private static string ResolveKind(JsonElement root, long? rseq)
    {
        string? type = ReadString(root, "type");
        if (string.Equals(type, "request", StringComparison.OrdinalIgnoreCase))
        {
            return "request";
        }

        if (string.Equals(type, "response", StringComparison.OrdinalIgnoreCase))
        {
            return "response";
        }

        if (string.Equals(type, "broadcast", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "event", StringComparison.OrdinalIgnoreCase))
        {
            return "event";
        }

        if (HasEventName(root))
        {
            return "event";
        }

        if (HasCallName(root))
        {
            return "request";
        }

        if (rseq.HasValue)
        {
            return "response";
        }

        return "unknown";
    }

    private static string ResolveName(JsonElement root, string kind, string? fallbackName)
    {
        string? name = kind switch
        {
            "event" => ReadString(root, "msgtype")
                ?? ReadString(root, "event")
                ?? ReadString(root, "name"),
            _ => ReadString(root, "call")
                ?? ReadString(root, "request")
                ?? fallbackName,
        };

        return name ?? string.Empty;
    }

    private static bool HasCallName(JsonElement root)
    {
        return !string.IsNullOrWhiteSpace(ReadString(root, "call"))
            || !string.IsNullOrWhiteSpace(ReadString(root, "request"));
    }

    private static bool HasEventName(JsonElement root)
    {
        return !string.IsNullOrWhiteSpace(ReadString(root, "msgtype"))
            || !string.IsNullOrWhiteSpace(ReadString(root, "event"))
            || !string.IsNullOrWhiteSpace(ReadString(root, "name"));
    }

    private static long? TryReadInt64(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out long value) => value,
            JsonValueKind.String when long.TryParse(property.GetString(), out long value) => value,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
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
