using System.Text.Json.Nodes;

namespace XivLinker.Infrastructure.Overlay.Services;

internal static class OverlayPluginRequestFactory
{
    public static JsonObject CreateRequestPayload(
        string call,
        long sequence,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        JsonObject payload = new()
        {
            ["type"] = "request",
            ["call"] = call,
            ["rseq"] = sequence,
        };

        if (parameters is not null)
        {
            foreach ((string key, object? value) in parameters)
            {
                payload[key] = value is null ? null : System.Text.Json.JsonSerializer.SerializeToNode(value);
            }
        }

        return payload;
    }
}
