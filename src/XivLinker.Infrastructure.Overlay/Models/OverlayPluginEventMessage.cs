using System.Text.Json;

namespace XivLinker.Infrastructure.Overlay.Models;

public sealed class OverlayPluginEventMessage
{
    public required string MessageType { get; init; }

    public required JsonElement Payload { get; init; }

    public required string RawJson { get; init; }
}
